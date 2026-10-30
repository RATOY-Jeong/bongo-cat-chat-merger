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
        private TextComposition activeComposerComposition;
        private bool sendAfterComposition;
        private void InitializeComposerInput()
        {
            // Never force Korean/English, change the keyboard layout, or touch another app's IME.
            InputMethod.SetPreferredImeState(composer,InputMethodState.DoNotCare);
            InputMethod.SetPreferredImeConversionMode(composer,ImeConversionModeValues.DoNotCare);
            InputMethod.SetPreferredImeSentenceMode(composer,ImeSentenceModeValues.DoNotCare);
            // Clicking Send must not move the keyboard focus into a button that is immediately disabled.
            sendButton.Focusable=false;
            TextCompositionManager.AddPreviewTextInputStartHandler(composer,delegate(object sender,TextCompositionEventArgs args)
            {compositionGeneration++;sendAfterComposition=false;activeComposerComposition=args.TextComposition;imeComposing=true;});
            TextCompositionManager.AddPreviewTextInputUpdateHandler(composer,delegate(object sender,TextCompositionEventArgs args)
            {activeComposerComposition=args.TextComposition;imeComposing=true;});
            TextCompositionManager.AddPreviewTextInputHandler(composer,delegate
            {activeComposerComposition=null;if(imeComposing)FinishCompositionLater();});
            composer.LostKeyboardFocus+=delegate
            {sendAfterComposition=false;activeComposerComposition=null;if(imeComposing)FinishCompositionLater();};
        }
        private void ComposerPreviewKeyDown(object sender,KeyEventArgs args)
        {
            Key key=args.Key==Key.ImeProcessed?args.ImeProcessedKey:args.Key;
            if(key!=Key.Return)return;
            if((args.KeyboardDevice.Modifiers&ModifierKeys.Shift)!=0){sendAfterComposition=false;return;}
            // A plain Enter must never fall through to TextBox's AcceptsReturn behavior.
            args.Handled=true;
            if(args.IsRepeat)return;
            if(!imeComposing){SendComposer();return;}
            if(demo||!canSend||pendingRequest!=null||liveState==null)return;
            sendAfterComposition=true;
            // Let the IME commit the final syllable; send only after TextBox inserts it.
            // Complete is virtual so native IME compositions can finish their own context.
            if(activeComposerComposition!=null)
            {
                try {activeComposerComposition.Complete();}
                catch(ArgumentException){sendAfterComposition=false;}
            }
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
                bool send=sendAfterComposition;sendAfterComposition=false;
                if(send)SendComposer();
            }));
        }
        private void UpdateComposerLimit(int limit)
        {
            if(imeComposing){pendingComposerLimit=limit;return;}
            if(composer.MaxLength!=limit)composer.MaxLength=limit;
        }
    }
}
