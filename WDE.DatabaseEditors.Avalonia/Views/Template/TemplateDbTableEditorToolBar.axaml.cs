using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Prism.Commands;
using WDE.Common.Utils;

namespace WDE.DatabaseEditors.Avalonia.Views.Template
{
    public class TemplateDbTableEditorToolBar : UserControl
    {
        private ICommand focusCommand;
        private Window? attachedRoot = null;
        
        public TemplateDbTableEditorToolBar()
        {
            InitializeComponent();
            focusCommand = new DelegateCommand(() =>
            {
                TextBox tb = this.FindControl<TextBox>("SearchTextBox");
                tb?.Focus();
            });
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            attachedRoot = this.GetVisualRoot() as Window;
            if (attachedRoot != null)
            {
                attachedRoot.KeyBindings.Add(new KeyBinding()
                    {
                        Command = focusCommand,
                        Gesture = new KeyGesture(Key.F, AvaloniaLocator.Current
                            .GetService<PlatformHotkeyConfiguration>()?.CommandModifiers ?? KeyModifiers.Control)
                    });
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            if (attachedRoot != null)
            {
                attachedRoot.KeyBindings.RemoveIf(x => x.Command == focusCommand);
                attachedRoot = null;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}