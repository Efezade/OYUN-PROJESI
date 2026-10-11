using UnityEngine;
using TacticalRPG.Core;
namespace TacticalRPG.UI
{
    /// <summary>Shared HUD surfaces and typography, including legacy immediate-mode screens.</summary>
    public static class ModernGUI
    {
        private static int _hovered;
        private static GUISkin _skin;private static Texture2D _panel,_button,_hover,_pressed;
        public static void Reset(){if(_skin!=null)Object.Destroy(_skin);foreach(var t in new[]{_panel,_button,_hover,_pressed})if(t!=null)Object.Destroy(t);_skin=null;}
        public static GUISkin Skin
        {
            get
            {
                if(_skin!=null)return _skin;
                _skin=Object.Instantiate(GUI.skin);_skin.name="Kok Ahdi HUD";
                var theme=PresentationTheme.Current;
                Color panel=theme!=null?theme.panel:new Color(.075f,.105f,.119f,.98f);
                Color raised=theme!=null?theme.raised:new Color(.12f,.17f,.18f);
                Color accent=theme!=null?theme.accent:new Color(.71f,.57f,.32f);
                Color text=theme!=null?theme.text:new Color(.91f,.91f,.84f);
                _panel=Surface(panel,new Color(.24f,.31f,.31f,.8f));_button=Surface(raised,new Color(.30f,.39f,.37f));
                _hover=Surface(raised*1.35f,accent);_pressed=Surface(raised*.8f,accent);
                int font=theme!=null?theme.hudFontSize:15;
                _skin.label.fontSize=font;_skin.label.normal.textColor=text;_skin.label.padding=new RectOffset(2,2,2,2);
                _skin.box.normal.background=_panel;_skin.box.normal.textColor=text;_skin.box.border=new RectOffset(9,9,9,9);_skin.box.padding=new RectOffset(12,12,10,10);_skin.box.fontSize=font;
                var button=_skin.button;button.fontSize=font;button.fontStyle=FontStyle.Normal;button.wordWrap=true;
                button.normal.background=_button;button.hover.background=_hover;button.active.background=_pressed;button.focused.background=_hover;
                button.normal.textColor=button.hover.textColor=button.active.textColor=button.focused.textColor=text;
                button.border=new RectOffset(9,9,9,9);button.padding=new RectOffset(10,10,4,4);button.margin=new RectOffset(3,3,4,4);
                _skin.horizontalSlider.normal.background=_button;_skin.horizontalSlider.border=new RectOffset(9,9,9,9);
                _skin.horizontalSliderThumb.normal.background=_hover;_skin.horizontalSliderThumb.fixedWidth=18;
                _skin.verticalScrollbar.normal.background=_panel;_skin.verticalScrollbarThumb.normal.background=_hover;
                _skin.verticalScrollbarThumb.border=new RectOffset(9,9,9,9);_skin.verticalScrollbar.fixedWidth=10;
                return _skin;
            }
        }
        private static Texture2D Surface(Color fill,Color border)
        {
            const int size=32;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                Vector2 q=new Vector2(Mathf.Abs(x-15.5f)-8,Mathf.Abs(y-15.5f)-8);
                float d=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-7;
                Color c=Color.Lerp(fill,border,Mathf.SmoothStep(-1.6f,-.3f,d));c.a*=1-Mathf.SmoothStep(-.5f,.5f,d);texture.SetPixel(x,y,c);
            }
            texture.Apply();return texture;
        }
        public static bool Button(string label,params GUILayoutOption[] options)
        {bool clicked=GUILayout.Button(label,options);Hover(GUILayoutUtility.GetLastRect(),label.GetHashCode());if(clicked)SoundSignals.Play("ui_click");return clicked;}
        public static bool Button(GUIContent label,params GUILayoutOption[] options)
        {bool clicked=GUILayout.Button(label,options);Hover(GUILayoutUtility.GetLastRect(),label.GetHashCode());if(clicked)SoundSignals.Play("ui_click");return clicked;}
        private static void Hover(Rect rect,int id)
        {if(Event.current.type!=EventType.Repaint)return;bool inside=GUI.enabled&&rect.Contains(Event.current.mousePosition);if(inside&&_hovered!=id){_hovered=id;SoundSignals.Play("ui_hover",.3f);}else if(!inside&&_hovered==id)_hovered=0;}
        public static bool Button(Rect rect,string label,GUIStyle style)
        {bool clicked=GUI.Button(rect,label,style);if(clicked)SoundSignals.Play("ui_click");return clicked;}
    }
}
