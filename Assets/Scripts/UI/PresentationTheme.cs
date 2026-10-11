using UnityEngine;
using UnityEngine.UI;
using TacticalRPG.Data;
namespace TacticalRPG.UI
{
    [DefaultExecutionOrder(-180)]
    public sealed class PresentationTheme : MonoBehaviour
    {
        [SerializeField] private PresentationThemeSO _theme;
        public const int CurrentRevision=3;
        [SerializeField] private int _revision=1;
        public int Revision=>_revision;
        public static PresentationThemeSO Current{get;private set;}
        private void Awake(){Current=_theme;ModernGUI.Reset();}
        private void OnDestroy(){if(Current==_theme)Current=null;ModernGUI.Reset();}
        public static void StyleControl(Selectable control)
        {
            if(control==null||Current==null)return;
            var colors=control.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.22f,1.27f,1.22f);
            colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.70f,.83f,.78f);
            colors.disabledColor=new Color(.45f,.49f,.48f,.5f);colors.fadeDuration=.14f;control.colors=colors;
        }
    }
}
