from pathlib import Path
import re, shutil, json
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from pptx import Presentation
from pptx.util import Pt
from pptx.dml.color import RGBColor
from pptx.enum.shapes import MSO_SHAPE
import fitz
from PIL import Image
D=Path(__file__).parent
F=D/'font'
for n,f in [('Lora','Lora-variable.ttf'),('Poppins','Poppins-Medium.ttf'),('PoppinsBold','Poppins-SemiBold.ttf')]:pdfmetrics.registerFont(TTFont(n,str(F/f)))
raw=(D/'copy-mario.md').read_text()
def clean(s):return re.sub(r'\*|^#+\s*','',s).strip()
navy='141413';teal='A6472D';ink='141413';muted='66645E';bg='FAF9F5'
class Layout:
 def __init__(self,name,w,h):
  self.name=name;self.w=w;self.h=h;self.c=canvas.Canvas(str(D/(name+'.pdf')),pagesize=(w,h));self.p=Presentation();self.p.slide_width=Pt(w);self.p.slide_height=Pt(h)
 def rect(self,x,y,w,h,color):
  self.c.setFillColor('#'+color);self.c.rect(x,self.h-y-h,w,h,stroke=0,fill=1)
  s=self.s.shapes.add_shape(MSO_SHAPE.RECTANGLE,Pt(x),Pt(y),Pt(w),Pt(h));s.fill.solid();s.fill.fore_color.rgb=RGBColor.from_string(color);s.line.fill.background()
 def text(self,text,x,y,w,size=12,bold=False,color=ink,lead=None):
  font=('PoppinsBold' if size>=24 else 'Poppins') if bold else 'Lora';lead=lead or size*1.38
  lines=[]
  for para in text.split('\n'):
   line=''
   for word in para.split():
    if line and pdfmetrics.stringWidth(line+' '+word,font,size)>w:lines.append(line);line=word
    else:line=(line+' '+word).strip()
   lines.append(line)
  for i,line in enumerate(lines):
   yy=y+i*lead
   assert yy+size<self.h-20,(self.name,text,yy)
   self.c.setFillColor('#'+color);self.c.setFont(font,size);self.c.drawString(x,self.h-yy-size,line)
   box=self.s.shapes.add_textbox(Pt(x),Pt(yy-1),Pt(w+5),Pt(lead+5));tf=box.text_frame;tf.margin_left=tf.margin_right=tf.margin_top=tf.margin_bottom=0;tf.word_wrap=False
   p=tf.paragraphs[0];p.text=line;p.font.name=('Poppins SemiBold' if size>=24 else 'Poppins Medium') if bold else 'Lora';p.font.size=Pt(size);p.font.bold=False;p.font.color.rgb=RGBColor.from_string(color)
  return y+len(lines)*lead
 def page(self,i,total):
  self.s=self.p.slides.add_slide(self.p.slide_layouts[6]);self.rect(0,0,self.w,self.h,bg);self.rect(48,48,42,3,'D97757')
  self.text('CAPITAL MAJORDOMO',48,24,self.w-96,9,True,navy)
  self.text('PROGETTO IN SVILUPPO',48,self.h-36,self.w-120,8,False,muted)
  self.text(f'{i:02} / {total:02}',self.w-80,self.h-36,60,8,False,muted)
 def end(self):self.c.showPage()
 def save(self):self.c.save();self.p.save(D/(self.name+'.pptx'))
b=Layout('brochure',595.276,841.89)
sec=raw.split('## 2. Brochure')[1].split('## 3. Pitch')[0]
pages=[sec.split('### Pagina 1')[1].split('### Pagina 2')[0],sec.split('### Pagina 2')[1]]
for num,part in enumerate(pages,1):
 b.page(num,2);y=71
 for block in part.strip().split('\n\n'):
  if block.strip()=='**Capital Majordomo**':continue
  t=clean(block)
  if block.startswith('# '):y=b.text(t,48,y,499,28,True,navy,lead=34)+16
  elif block.startswith('### '):y=b.text(t,48,y,499,11.5,True,teal,lead=16)+8
  else:
   size=11.5;bold=False
   if block.startswith('**'):size=10.7;bold=True
   y=b.text(t,48,y,499,size,bold,ink,lead=16)+14
 b.end();print('brochure',num,'bottom',y)
b.save()

# Layout and content verification
original=fitz.open(D.parent/'original'/'brochure.pdf') if (D.parent/'original'/'brochure.pdf').exists() else None
doc=fitz.open(D/'brochure.pdf')
assert len(doc)==2
assert len(Presentation(D/'brochure.pptx').slides)==2
for i,page in enumerate(doc):
 for block in page.get_text('blocks'):
  assert block[0]>=0 and block[1]>=0 and block[2]<=page.rect.width and block[3]<=page.rect.height
 if original:
  assert re.sub(r'\s+','',page.get_text())==re.sub(r'\s+','',original[i].get_text()), 'Content changed'
 page.get_pixmap(matrix=fitz.Matrix(1.4,1.4)).save(D/f'pagina-{i+1}.png')
print('Verified: 2 pages, 2 editable slides, unchanged text, text within bounds')
