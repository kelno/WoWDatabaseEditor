using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using Prism.Commands;
using Prism.Events;
using WDE.Common;
using WDE.Common.Database;
using WDE.Common.History;
using WDE.Common.Parameters;
using WDE.Common.Providers;
using WDE.Common.Services;
using WDE.Common.Services.MessageBox;
using WDE.Common.Sessions;
using WDE.Common.Solution;
using WDE.Common.Tasks;
using WDE.Common.Utils;
using WDE.DatabaseEditors.Data.Interfaces;
using WDE.DatabaseEditors.Data.Structs;
using WDE.DatabaseEditors.Extensions;
using WDE.DatabaseEditors.History;
using WDE.DatabaseEditors.Loaders;
using WDE.DatabaseEditors.Models;
using WDE.DatabaseEditors.QueryGenerators;
using WDE.DatabaseEditors.Solution;
using WDE.MVVM;
using WDE.MVVM.Observable;

namespace WDE.DatabaseEditors.ViewModels.MultiRow
{
    public class MultiRowDbTableEditorViewModel : ViewModelBase
    {
        private readonly IItemFromListProvider itemFromListProvider;
        private readonly IMessageBoxService messageBoxService;
        private readonly IParameterFactory parameterFactory;
        private readonly IMySqlExecutor mySqlExecutor;
        private readonly IQueryGenerator queryGenerator;
        private readonly IDatabaseTableModelGenerator modelGenerator;
        private readonly IConditionEditService conditionEditService;
        private readonly IDatabaseTableDataProvider tableDataProvider;

        private Dictionary<uint, DatabaseEntitiesGroupViewModel> byEntryGroups = new();
        public ObservableCollection<DatabaseEntitiesGroupViewModel> Rows { get; } = new();

        private IList<DatabaseColumnJson> columns = new List<DatabaseColumnJson>();
        public ObservableCollection<DatabaseColumnHeaderViewModel> Columns { get; } = new();
        private DatabaseColumnJson? autoIncrementColumn;

        private HashSet<uint> keys = new();

        public AsyncAutoCommand AddNewCommand { get; }
        public AsyncAutoCommand<DatabaseCellViewModel?> RemoveTemplateCommand { get; }
        public AsyncAutoCommand<DatabaseCellViewModel?> RevertCommand { get; }
        public AsyncAutoCommand<DatabaseCellViewModel?> EditConditionsCommand { get; }
        public DelegateCommand<DatabaseCellViewModel?> SetNullCommand { get; }
        public DelegateCommand<DatabaseCellViewModel?> DuplicateCommand { get; }
        public DelegateCommand<DatabaseEntitiesGroupViewModel> AddRowCommand { get; }
        public AsyncAutoCommand<DatabaseCellViewModel> OpenParameterWindow { get; }

        public event Action<DatabaseEntity>? OnDeletedQuery;
        public event Action<DatabaseEntity>? OnKeyDeleted;
        public event Action<uint>? OnKeyAdded;

        public MultiRowDbTableEditorViewModel(DatabaseTableSolutionItem solutionItem,
            IDatabaseTableDataProvider tableDataProvider, IItemFromListProvider itemFromListProvider,
            IHistoryManager history, ITaskRunner taskRunner, IMessageBoxService messageBoxService,
            IEventAggregator eventAggregator, ISolutionManager solutionManager, 
            IParameterFactory parameterFactory, ISolutionTasksService solutionTasksService,
            ISolutionItemNameRegistry solutionItemName, IMySqlExecutor mySqlExecutor,
            IQueryGenerator queryGenerator, IDatabaseTableModelGenerator modelGenerator,
            ITableDefinitionProvider tableDefinitionProvider,
            IConditionEditService conditionEditService, ISolutionItemIconRegistry iconRegistry,
            ISessionService sessionService) 
            : base(history, solutionItem, solutionItemName, 
            solutionManager, solutionTasksService, eventAggregator, 
            queryGenerator, tableDataProvider, messageBoxService, taskRunner, parameterFactory,
            tableDefinitionProvider, itemFromListProvider, iconRegistry, sessionService)
        {
            this.itemFromListProvider = itemFromListProvider;
            this.solutionItem = solutionItem;
            this.tableDataProvider = tableDataProvider;
            this.messageBoxService = messageBoxService;
            this.parameterFactory = parameterFactory;
            this.mySqlExecutor = mySqlExecutor;
            this.queryGenerator = queryGenerator;
            this.modelGenerator = modelGenerator;
            this.conditionEditService = conditionEditService;

            OpenParameterWindow = new AsyncAutoCommand<DatabaseCellViewModel>(EditParameter);
            RemoveTemplateCommand = new AsyncAutoCommand<DatabaseCellViewModel?>(RemoveTemplate, vm => vm != null);
            RevertCommand = new AsyncAutoCommand<DatabaseCellViewModel?>(Revert, cell => cell is DatabaseCellViewModel vm && vm.CanBeReverted && (vm.TableField?.IsModified ?? false));
            SetNullCommand = new DelegateCommand<DatabaseCellViewModel?>(SetToNull, vm => vm != null && vm.CanBeSetToNull);
            DuplicateCommand = new DelegateCommand<DatabaseCellViewModel?>(Duplicate, vm => vm != null);
            EditConditionsCommand = new AsyncAutoCommand<DatabaseCellViewModel?>(EditConditions);
            AddRowCommand = new DelegateCommand<DatabaseEntitiesGroupViewModel>(AddRowByGroup);
            AddNewCommand = new AsyncAutoCommand(AddNewEntity);
            
            ScheduleLoading();
        }

        public DatabaseEntity AddRow(uint key)
        {
            var freshEntity = modelGenerator.CreateEmptyEntity(tableDefinition, key);
            if (autoIncrementColumn != null)
            {
                long max = 0;
                
                if (byEntryGroups[key].Count > 0)
                    max = 1 + byEntryGroups[key].Max(t =>
                    {
                        if (t.Entity.GetCell(autoIncrementColumn.DbColumnName) is DatabaseField<long> lField)
                            return lField.Current.Value;
                        return 0L;
                    });
                
                if (freshEntity.GetCell(autoIncrementColumn.DbColumnName) is DatabaseField<long> lField)
                    lField.Current.Value = max;
            }
            ForceInsertEntity(freshEntity, Entities.Count);
            return freshEntity;
        }
        
        private void AddRowByGroup(DatabaseEntitiesGroupViewModel group)
        {
            AddRow(group.Key);
        }

        private async Task AddNewEntity()
        {
            var parameter = parameterFactory.Factory(tableDefinition.Picker);
            var selected = await itemFromListProvider.GetItemFromList(parameter.Items, false);
            if (!selected.HasValue)
                return;

            uint key = (uint) selected;

            if (ContainsKey(key))
            {
                await messageBoxService.ShowDialog(new MessageBoxFactory<bool>()
                    .SetTitle("Key already added")
                    .SetMainInstruction($"Key {key} is already added to this editor")
                    .SetContent("To add a new row, click (+) sign next to the key name")
                    .WithOkButton(true)
                    .SetIcon(MessageBoxIcon.Warning)
                    .Build());
                return;
            }
            
            var data = await tableDataProvider.Load(tableDefinition.Id, key);
            if (data == null) 
                return;

            OnKeyAdded?.Invoke(key);
            
            EnsureKey(key);

            foreach (var entity in data.Entities)
                await AddEntity(entity);
        }
        
        private void SetToNull(DatabaseCellViewModel? view)
        {
            if (view != null && view.CanBeNull && !view.IsReadOnly) 
                view.ParameterValue?.SetNull();
        }

        private void Duplicate(DatabaseCellViewModel? view)
        {
            if (view != null)
            {
                var duplicate = view.Parent.Entity.Clone();
                ForceInsertEntity(duplicate, 0);
            }
        }

        private async Task EditConditions(DatabaseCellViewModel? view)
        {
            if (view == null)
                return;
            
            var conditionList = view.ParentEntity.Conditions;
            
            var newConditions = await conditionEditService.EditConditions(tableDefinition.Condition!.SourceType, conditionList);
            if (newConditions == null)
                return;

            view.ParentEntity.Conditions = newConditions.ToList();
            if (tableDefinition.Condition.SetColumn != null)
            {
                var hasColumn = view.ParentEntity.GetCell(tableDefinition.Condition.SetColumn);
                if (hasColumn is DatabaseField<long> lf)
                    lf.Current.Value = view.ParentEntity.Conditions.Count > 0 ? 1 : 0;
            }
        }
        
        private async Task Revert(DatabaseCellViewModel? view)
        {
            if (view == null || view.IsReadOnly)
                return;
            
            view.ParameterValue?.Revert();
        }

        private async Task RemoveTemplate(DatabaseCellViewModel? view)
        {
            if (view == null)
                return;

            await RemoveEntity(view.ParentEntity);
        }

        private Task EditParameter(DatabaseCellViewModel cell)
        {
            if (cell.ParameterValue != null)
                return EditParameter(cell.ParameterValue);
            return Task.CompletedTask;
        }

        protected override ICollection<uint> GenerateKeys() => keys;

        protected override async Task InternalLoadData(DatabaseTableData data)
        {
            Rows.Clear();
            columns = tableDefinition.Groups.SelectMany(g => g.Fields)
                .Where(c => c.DbColumnName != data.TableDefinition.TablePrimaryKeyColumnName)
                .ToList();
            autoIncrementColumn = columns.FirstOrDefault(c => c.AutoIncrement);
            Columns.Clear();
            Columns.AddRange(columns.Select(c => new DatabaseColumnHeaderViewModel(c)));
            
            foreach (var entity in solutionItem.Entries)
                EnsureKey(entity.Key);

            await AsyncAddEntities(data.Entities);
            History.AddHandler(AutoDispose(new MultiRowTableEditorHistoryHandler(this)));
        }

        protected override void UpdateSolutionItem()
        {
            solutionItem.Entries = keys.Select(e =>
                new SolutionItemDatabaseEntity(e, false)).ToList();
        }

        public async Task<bool> RemoveEntity(DatabaseEntity entity)
        {
            var itemsWithSameKey = Entities.Count(e => e.Key == entity.Key);

            var removed = ForceRemoveEntity(entity);
            
            if (itemsWithSameKey == 1)
            {
                if (await messageBoxService.ShowDialog(new MessageBoxFactory<bool>()
                    .SetTitle("Removing entity")
                    .SetMainInstruction($"Do you want to delete the key {entity.Key} from solution?")
                    .SetContent(
                        $"This entity is the last row with key {entity.Key}. You have to choose if you want to delete the key from the solution as well.\n\nIf you delete it from the solution, DELETE FROM... will no longer be generated for this key.")
                    .WithYesButton(true)
                    .WithNoButton(false)
                    .SetIcon(MessageBoxIcon.Information)
                    .Build()))
                {
                    if (mySqlExecutor.IsConnected)
                    {
                        if (await messageBoxService.ShowDialog(new MessageBoxFactory<bool>()
                            .SetTitle("Execute DELETE query?")
                            .SetMainInstruction("Do you want to execute DELETE query now?")
                            .SetContent(
                                "You have decided to remove the item from solution, therefore DELETE FROM query will not be generated for this key anymore, you we can execute DELETE with that key for that last time.")
                            .WithYesButton(true)
                            .WithNoButton(false)
                            .Build()))
                        {
                            OnDeletedQuery?.Invoke(entity);
                            await mySqlExecutor.ExecuteSql(queryGenerator.GenerateDeleteQuery(tableDefinition, entity));
                            History.MarkNoSave();
                        }
                    }
                    RemoveKey(entity.Key);
                    OnKeyDeleted?.Invoke(entity);
                }
            }
            
            return removed;
        }
        
        public void RedoExecuteDelete(DatabaseEntity entity)
        {
            if (mySqlExecutor.IsConnected)
            {
                mySqlExecutor.ExecuteSql(queryGenerator.GenerateDeleteQuery(tableDefinition, entity));
                History.MarkNoSave();
            }
        }
        
        public void DoAddKey(uint entity)
        {
            EnsureKey(entity);
        }

        public void UndoAddKey(uint entity)
        {
            RemoveKey(entity);
        }
        
        public override bool ForceRemoveEntity(DatabaseEntity entity)
        {
            var indexOfEntity = Entities.IndexOf(entity);
            if (indexOfEntity == -1)
                return false;
            
            Entities.RemoveAt(indexOfEntity);
            byEntryGroups[entity.Key].Remove(entity);

            return true;
        }
        
        public async Task<bool> AddEntity(DatabaseEntity entity)
        {
            return ForceInsertEntity(entity, Entities.Count);
        }

        public override bool ForceInsertEntity(DatabaseEntity entity, int index)
        {
            var name = parameterFactory.Factory(tableDefinition.Picker).ToString(entity.Key);
            var row = new DatabaseEntityViewModel(entity, name);
            
            int columnIndex = 0;
            foreach (var column in columns)
            {
                DatabaseCellViewModel cellViewModel;

                if (column.IsConditionColumn)
                {
                    var label = entity.ToObservable(e => e.Conditions).Select(c => "Edit (" + (c?.Count ?? 0) + ")");
                    cellViewModel = AutoDispose(new DatabaseCellViewModel(columnIndex, "conditions", EditConditionsCommand, row, entity, label));
                }
                else
                {
                    var cell = entity.GetCell(column.DbColumnName);
                    if (cell == null)
                        throw new Exception("this should never happen");

                    IParameterValue parameterValue = null!;
                    if (cell is DatabaseField<long> longParam)
                    {
                        parameterValue = new ParameterValue<long>(longParam.Current, longParam.Original, parameterFactory.Factory(column.ValueType));
                    }
                    else if (cell is DatabaseField<string> stringParam)
                    {
                        if (column.AutogenerateComment != null)
                        {
                            stringParam.Current.Value = stringParam.Current.Value.GetComment(column.CanBeNull);
                            stringParam.Original.Value = stringParam.Original.Value.GetComment(column.CanBeNull);
                        }
                        parameterValue = new ParameterValue<string>(stringParam.Current, stringParam.Original, parameterFactory.FactoryString(column.ValueType));
                    }
                    else if (cell is DatabaseField<float> floatParameter)
                    {
                        parameterValue = new ParameterValue<float>(floatParameter.Current, floatParameter.Original, FloatParameter.Instance);
                    }

                    cellViewModel = AutoDispose(new DatabaseCellViewModel(columnIndex, column, row, entity, cell, parameterValue));
                }
                row.Cells.Add(cellViewModel);
                columnIndex++;
            }
            
            Entities.Insert(index, entity);
            EnsureKey(entity.Key);
            
            byEntryGroups[entity.Key].Add(row);
            return true;
        }

        private void RemoveKey(uint entity)
        {
            if (keys.Remove(entity))
            {
                Rows.Remove(byEntryGroups[entity]);
                byEntryGroups.Remove(entity);
            }
        }

        private bool ContainsKey(uint key)
        {
            return keys.Contains(key);
        }
        
        private void EnsureKey(uint entity)
        {
            if (keys.Add(entity))
            {
                byEntryGroups[entity] = new DatabaseEntitiesGroupViewModel(entity, GenerateName(entity));
                Rows.Add(byEntryGroups[entity]);
            }
        }

        private async Task AsyncAddEntities(IList<DatabaseEntity> tableDataEntities)
        {
            List<DatabaseEntity> finalList = new();
            foreach (var entity in tableDataEntities)
            {
                if (await AddEntity(entity))
                    finalList.Add(entity);
            }
        }
    }
}