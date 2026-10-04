using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Collections.Generic;

class BrandBuilder {
 static GraphicsPath Tile(){var p=new GraphicsPath();p.StartFigure();p.AddLine(28,3,72,3);p.AddBezier(72,3,91,3,97,9,97,28);p.AddLine(97,28,97,72);p.AddBezier(97,72,97,91,91,97,72,97);p.AddLine(72,97,28,97);p.AddBezier(28,97,9,97,3,91,3,72);p.AddLine(3,72,3,28);p.AddBezier(3,28,3,9,9,3,28,3);p.CloseFigure();return p;}
 static Bitmap Draw(int size){
  using(var large=new Bitmap(size*4,size*4,PixelFormat.Format32bppArgb)){
   using(var g=Graphics.FromImage(large)){g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(large.Width/100f,large.Height/100f);
    using(var tile=Tile()){
     using(var b=new LinearGradientBrush(new PointF(8,6),new PointF(94,98),Color.Empty,Color.Empty)){b.InterpolationColors=new ColorBlend{Colors=new[]{Color.FromArgb(102,82,221),Color.FromArgb(167,103,210),Color.FromArgb(235,142,183)},Positions=new[]{0f,.52f,1f}};g.FillPath(b,tile);}
     g.SetClip(tile);using(var light=new LinearGradientBrush(new RectangleF(0,0,100,100),Color.FromArgb(50,255,255,255),Color.Transparent,65))g.FillEllipse(light,-22,-45,138,109);g.ResetClip();
     using(var edge=new Pen(Color.FromArgb(80,255,255,255),.65f))g.DrawPath(edge,tile);
    }
    var inkState=g.Save();g.TranslateTransform(1,0);g.ScaleTransform(.9f,1);
    using(var pen=new Pen(Color.White,6.5f)){pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
     using(var s=new GraphicsPath()){s.AddBezier(34,38,31,33,20,32,17,38);s.AddBezier(17,38,12,47,22,48,27,50);s.AddBezier(27,50,39,54,35,64,27,66);s.AddBezier(27,66,22,68,17,64,15,62);g.DrawPath(pen,s);}
     using(var u=new GraphicsPath()){u.AddLine(44,35,44,55);u.AddBezier(44,55,44,70,63,70,63,55);u.AddLine(63,55,63,35);g.DrawPath(pen,u);}
     using(var b=new GraphicsPath()){b.AddLine(74,35,74,66);b.StartFigure();b.AddLine(74,35,82,35);b.AddBezier(82,35,94,35,94,49,82,50);b.AddLine(82,50,74,50);b.StartFigure();b.AddLine(74,50,82,50);b.AddBezier(82,50,97,50,96,66,82,66);b.AddLine(82,66,74,66);g.DrawPath(pen,b);}
    }
    g.Restore(inkState);using(var pen=new Pen(Color.FromArgb(195,255,240,246),3.2f)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawLine(pen,34,79,66,79);}
   }
   var result=new Bitmap(size,size,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(result)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.CompositingMode=CompositingMode.SourceCopy;g.DrawImage(large,new Rectangle(0,0,size,size));}return result;
  }
 }
 static byte[] Dib(Bitmap image){int size=image.Width,maskStride=((size+31)/32)*4;using(var ms=new MemoryStream())using(var w=new BinaryWriter(ms)){w.Write(40);w.Write(size);w.Write(size*2);w.Write((short)1);w.Write((short)32);w.Write(0);w.Write(size*size*4+maskStride*size);w.Write(0);w.Write(0);w.Write(0);w.Write(0);for(int y=size-1;y>=0;y--)for(int x=0;x<size;x++){Color c=image.GetPixel(x,y);w.Write(c.B);w.Write(c.G);w.Write(c.R);w.Write(c.A);}for(int y=size-1;y>=0;y--){byte[] mask=new byte[maskStride];for(int x=0;x<size;x++)if(image.GetPixel(x,y).A==0)mask[x/8]|=(byte)(128>>(x%8));w.Write(mask);}return ms.ToArray();}}
 static int Main(string[] args){string dir=Path.GetFullPath(args[0]);Directory.CreateDirectory(dir);int[] sizes={16,24,32,48,64,128,256};var data=new List<byte[]>();foreach(int size in sizes)using(var b=Draw(size)){data.Add(Dib(b));if(size==32)b.Save(Path.Combine(dir,"sub-32.png"),ImageFormat.Png);}using(var b=Draw(1024))b.Save(Path.Combine(dir,"sub.png"),ImageFormat.Png);
  using(var stream=File.Create(Path.Combine(dir,"sub.ico")))using(var w=new BinaryWriter(stream)){w.Write((short)0);w.Write((short)1);w.Write((short)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)0);w.Write((byte)0);w.Write((short)1);w.Write((short)32);w.Write(data[i].Length);w.Write(offset);offset+=data[i].Length;}foreach(byte[] bytes in data)w.Write(bytes);}
  string svg="<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><defs><linearGradient id=\"g\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\"><stop stop-color=\"#6652DD\"/><stop offset=\".52\" stop-color=\"#A767D2\"/><stop offset=\"1\" stop-color=\"#EB8EB7\"/></linearGradient><linearGradient id=\"l\" x2=\".3\" y2=\"1\"><stop stop-color=\"white\" stop-opacity=\".2\"/><stop offset=\"1\" stop-color=\"white\" stop-opacity=\"0\"/></linearGradient><clipPath id=\"clip\"><path id=\"tile\" d=\"M28 3H72C91 3 97 9 97 28V72C97 91 91 97 72 97H28C9 97 3 91 3 72V28C3 9 9 3 28 3Z\"/></clipPath></defs><use href=\"#tile\" fill=\"url(#g)\"/><ellipse cx=\"47\" cy=\"9.5\" rx=\"69\" ry=\"54.5\" fill=\"url(#l)\" clip-path=\"url(#clip)\"/><use href=\"#tile\" fill=\"none\" stroke=\"white\" stroke-opacity=\".31\" stroke-width=\".65\"/><g fill=\"none\" stroke=\"white\" stroke-width=\"6.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M34 38C31 33 20 32 17 38C12 47 22 48 27 50C39 54 35 64 27 66C22 68 17 64 15 62\"/><path d=\"M44 35V55C44 70 63 70 63 55V35\"/><path d=\"M74 35V66M74 35H82C94 35 94 49 82 50H74M74 50H82C97 50 96 66 82 66H74\"/></g><path d=\"M34 79H66\" fill=\"none\" stroke=\"#FFF0F6\" stroke-opacity=\".76\" stroke-width=\"3.2\" stroke-linecap=\"round\"/></svg>";
  svg=svg.Replace("<g fill=","<g transform=\"translate(1 0) scale(.9 1)\" fill=");File.WriteAllText(Path.Combine(dir,"sub.svg"),svg,new UTF8Encoding(false));Console.WriteLine("SUB SVG, PNG and ICO generated: "+dir);return 0;
 }
}
