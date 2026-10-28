using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
namespace BongoChat
{
    public sealed partial class ChatWindow
    {
        private bool deferredRender,deferredScroll;
        private int pendingComposerLimit=-1,compositionGeneration;
        private void InitializeComposerInput()
        {
            // Never force Korean/English, change the keyboard layout, or touch another app's IME.
            InputMethod.SetPreferredImeState(composer,InputMethodState.DoNotCare);
            InputMethod.SetPreferredImeConversionMode(composer,ImeConversionModeValues.DoNotCare);
            InputMethod.SetPreferredImeSentenceMode(composer,ImeSentenceModeValues.DoNotCare);
            // Clicking Send must not move the keyboard focus into a button that is immediately disabled.
            sendButton.Focusable=false;
            TextCompositionManager.AddPreviewTextInputStartHandler(composer,delegate {compositionGeneration++;imeComposing=true;});
            TextCompositionManager.AddPreviewTextInputUpdateHandler(composer,delegate {imeComposing=true;});
            TextCompositionManager.AddPreviewTextInputHandler(composer,delegate {if(imeComposing)FinishCompositionLater();});
            composer.LostKeyboardFocus+=delegate {if(imeComposing)FinishCompositionLater();};
        }
        private void FinishCompositionLater()
        {
            int generation=compositionGeneration;
            // PreviewTextInput runs before TextBox inserts committed text. Never clear/rebuild it there.
            Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(delegate
            {
                if(generation!=compositionGeneration)return;
                imeComposing=false;
                if(pendingComposerLimit>0){int value=pendingComposerLimit;pendingComposerLimit=-1;UpdateComposerLimit(value);}
                RefreshComposer();
                if(deferredRender){bool scroll=deferredScroll;deferredRender=deferredScroll=false;RenderMessages(scroll);}
            }));
        }
        private void UpdateComposerLimit(int limit)
        {
            if(imeComposing){pendingComposerLimit=limit;return;}
            if(composer.MaxLength!=limit)composer.MaxLength=limit;
        }
    }
}