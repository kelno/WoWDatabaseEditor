using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Prism.Events;
using WDE.Common;
using WDE.Common.CoreVersion;
using WDE.Common.Events;
using WDE.Common.Managers;
using WDE.Common.Solution;
using WDE.Common.Types;
using WDE.Common.Utils;
using WDE.MVVM;
using WDE.MVVM.Observable;

namespace WoWDatabaseEditorCore.ViewModels
{
    public class RelatedSolutionItems : ObservableBase
    {
        private readonly IDocumentManager documentManager;
        private readonly ISolutionItemProvideService provideService;
        private readonly ISolutionItemRelatedRegistry relatedRegistry;
        private readonly IEventAggregator eventAggregator;
        private readonly ICurrentCoreVersion currentCoreVersion;
        public ObservableCollection<ViewModel> List { get; } = new();

        private CancellationTokenSource? tokenSource;
        
        public RelatedSolutionItems(IDocumentManager documentManager,
            ISolutionItemProvideService provideService,
            ISolutionItemRelatedRegistry relatedRegistry,
            IEventAggregator eventAggregator,
            ISolutionManager solutionManager,
            ICurrentCoreVersion currentCoreVersion)
        {
            this.documentManager = documentManager;
            this.provideService = provideService;
            this.relatedRegistry = relatedRegistry;
            this.eventAggregator = eventAggregator;
            this.currentCoreVersion = currentCoreVersion;
            solutionManager.RefreshRequest += _ => DoProcess(documentManager.ActiveSolutionItemDocument);
            documentManager.ToObservable(t => t.ActiveSolutionItemDocument).SubscribeAction(DoProcess);
            eventAggregator.GetEvent<DatabaseCacheReloaded>()
                .Subscribe(_ => DoProcess(documentManager.ActiveSolutionItemDocument), ThreadOption.UIThread, true);
        }

        private void DoProcess(ISolutionItemDocument? item)
        {
            tokenSource?.Cancel();
            tokenSource = new CancellationTokenSource();
            Process(item?.SolutionItem, tokenSource.Token).ListenErrors();
        }

        private async Task Process(ISolutionItem? item, CancellationToken token)
        {
            List.Clear();

            if (item == null)
                return;
            
            var related = await relatedRegistry.GetRelated(item);
            if (related == null)
                return;

            foreach (var provider in provideService.GetRelatedCreators(related.Value))
            {
                if (!provider.ShowInQuickStart(currentCoreVersion.Current))
                    continue;

                if (token.IsCancellationRequested)
                    return;
                
                List.Add(new ViewModel(provider.GetName(), provider.GetImage(), new AsyncAutoCommand(async () =>
                {
                    var newItem = await provider.CreateRelatedSolutionItem(related.Value);
                    if (newItem == null)
                        return;
                    eventAggregator.GetEvent<EventRequestOpenItem>().Publish(newItem);
                })));
            }
        }
        
        public class ViewModel
        {
            public ViewModel(string name, ImageUri icon, ICommand createCommand)
            {
                Name = name;
                Icon = icon;
                CreateCommand = createCommand;
            }

            public string Name { get; }
            public ImageUri Icon { get; }
            public ICommand CreateCommand { get; }
        }
    }
}