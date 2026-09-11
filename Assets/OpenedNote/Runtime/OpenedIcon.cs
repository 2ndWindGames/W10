using UnityEngine;
using UnityEngine.UIElements;

namespace OpenedNote
{
    public sealed class OpenedIcon : VisualElement
    {
        readonly string kind;
        public Color Ink = new Color32(232, 92, 38, 255);
        public OpenedIcon(string kind, int size = 24)
        {
            this.kind = kind; style.width = size; style.height = size; style.flexShrink = 0; pickingMode = PickingMode.Ignore; generateVisualContent += Draw;
        }
        void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D; float scale = contentRect.width / 24f;
            p.strokeColor = Ink; p.fillColor = Ink; p.lineWidth = 1.55f * scale; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
            Vector2 V(float x, float y) => new Vector2(x * scale, y * scale);
            void Line(params float[] points) { p.BeginPath(); p.MoveTo(V(points[0], points[1])); for (int i = 2; i < points.Length; i += 2) p.LineTo(V(points[i], points[i + 1])); p.Stroke(); }
            void Box(float x, float y, float w, float h) { Line(x,y,x+w,y,x+w,y+h,x,y+h,x,y); }
            void Circle(float x, float y, float r) { p.BeginPath(); p.Arc(V(x,y),r*scale,0,360); p.Stroke(); }
            void Fill(Color color, params float[] points) { p.fillColor = color; p.BeginPath(); p.MoveTo(V(points[0],points[1])); for (int i=2;i<points.Length;i+=2)p.LineTo(V(points[i],points[i+1]));p.ClosePath();p.Fill(); }
            var blue = new Color32(88, 173, 201, 255); var pale = new Color32(244, 250, 248, 255); var terracotta = new Color32(205, 82, 46, 255);
            switch (kind)
            {
                case "back": Line(15,5,8,12,15,19); break;
                case "next": Line(9,6,15,12,9,18); break;
                case "plus": Line(12,5,12,19); Line(5,12,19,12); break;
                case "close": Line(6,6,18,18); Line(18,6,6,18); break;
                case "home": Line(3,11,12,3,21,11); Line(5,10,5,21,10,21,10,15,14,15,14,21,19,21,19,10); break;
                case "settings": Circle(12,12,4); Circle(12,12,8); for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Line(12+Mathf.Cos(a)*8,12+Mathf.Sin(a)*8,12+Mathf.Cos(a)*10,12+Mathf.Sin(a)*10);} break;
                case "archive": Box(3,3,18,5);Box(5,8,14,13);Line(10,12,14,12);break;
                case "milk":
                    p.lineWidth = .65f * scale; p.strokeColor = blue;
                    Fill(pale,5,9,16,9,16,22,5,22);Fill(blue,5,9,8,4,16,4,19,9,16,10);Fill(new Color32(218,236,239,255),16,10,19,9,19,21,16,22);
                    Box(8,2,8,2);Line(5,9,5,22,16,22,19,21,19,9,16,4);Circle(10.5f,15,2);Line(9,18,13,18);break;
                case "tube":
                    p.strokeColor = new Color32(222,153,47,255);p.lineWidth=.75f*scale;
                    Fill(pale,6,2,18,2,16,19,8,19);Fill(new Color32(246,187,80,255),8,19,16,19,16,22,8,22);
                    Line(6,2,18,2,16,19,8,19,6,2);Box(8,19,8,3);Circle(12,10,2.4f);
                    for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Line(12+Mathf.Cos(a)*3.5f,10+Mathf.Sin(a)*3.5f,12+Mathf.Cos(a)*4.4f,10+Mathf.Sin(a)*4.4f);}break;
                case "box":
                    Fill(new Color32(222,200,173,255),4,7,12,3,20,7,20,20,12,23,4,20);p.strokeColor=new Color32(144,116,86,255);p.lineWidth=.8f*scale;
                    Line(4,7,12,11,20,7);Line(12,11,12,23);Line(4,7,4,20,12,23,20,20,20,7,12,3,4,7);Line(8,5,16,9);break;
                default:
                    if(contentRect.width > 30) {
                        p.lineWidth=.65f*scale;p.strokeColor=terracotta;Fill(new Color32(218,104,65,255),6,6,18,6,19,9,19,22,5,22,5,9);
                        Fill(new Color32(245,229,204,255),6,11,18,11,18,19,6,19); Fill(new Color32(100,93,78,255),5,2,19,2,19,5,5,5);
                        p.strokeColor=new Color32(141,151,88,255);Line(10,15,12,13,14,15);p.strokeColor=terracotta;Circle(12,16,2);
                    } else { Box(6,3,12,3);Line(7,6,5,9,5,21,19,21,19,9,17,6);Line(9,13,12,16,16,11); }
                    break;
            }
        }
    }
}
