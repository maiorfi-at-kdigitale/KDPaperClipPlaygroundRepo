"""Generate A4 digital flyer and its 300 dpi PNG from the same PDF."""
from pathlib import Path
import math
from reportlab.pdfgen import canvas
from reportlab.lib.colors import HexColor
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
import fitz
P=Path(__file__).resolve().parent
for name,file in [('P','Poppins-Medium.ttf'),('B','Poppins-SemiBold.ttf')]:
    pdfmetrics.registerFont(TTFont(name,str(P/'assets'/file)))
W,H=595.2756,841.8898
c=canvas.Canvas(str(P/'capital-majordomo.pdf'),pagesize=(W,H),pageCompression=1)
c.setTitle('Capital-Majordomo — Un laboratorio per le strategie')
c.setAuthor('Marketing · Capital-Majordomo')
def box(x,y,w,h,color,r=0):
    c.setFillColor(HexColor(color)); c.roundRect(x,H-y-h,w,h,r,stroke=0,fill=1)
def text(x,y,s,size=10,color='#242222',font='P'):
    c.setFillColor(HexColor(color));c.setFont(font,size);c.drawString(x,H-y,s)
def lines(x,y,ss,size=10,leading=15,color='#242222',font='P'):
    for i,s in enumerate(ss): text(x,y+i*leading,s,size,color,font)
def line(x1,y1,x2,y2,color,width=1):
    c.setStrokeColor(HexColor(color));c.setLineWidth(width);c.line(x1,H-y1,x2,H-y2)
box(0,0,W,H,'#ffffff')
text(36,43,'Capital-Majordomo',16,font='B')
box(365,26,194,25,'#f4f3f3',12)
text(378,42,'PLAYGROUND · IN SVILUPPO',8,font='B')
lines(36,104,['Un laboratorio','per le strategie.'],36,43,font='B')
lines(37,181,['Esplora un progetto di trading automatico pensato','per integrarsi con le API di Capital.com.'],11,17)
# Original vector composition: three connected experimental stages, no performance chart.
box(36,224,523,252,'#141212',22)
# fine orbit lines
c.setStrokeColor(HexColor('#494545'));c.setLineWidth(.65)
for offset in (0,18,36):
    c.ellipse(103-offset,H-446-offset*.12,489+offset,H-261+offset*.12,fill=0,stroke=1)
# connectors behind floating panels
line(165,329,289,372,'#bc8c62',2)
line(290,372,427,304,'#bc8c62',2)
for x,y,n in [(72,270,'01'),(221,329,'02'),(382,251,'03')]:
    box(x+5,y+7,112,114,'#050202',14)
    box(x,y,112,114,'#242222',14)
    text(x+15,y+27,n,12,'#d1cece','B')
    # each stage gets a different original abstract icon
    if n=='01':
        for j in range(3):
            for k in range(4): box(x+18+k*20,y+47+j*16,7,7,'#e8e3e3' if (j+k)%3 else '#bc8c62',2)
    elif n=='02':
        for j in range(3):
            line(x+18,y+52+j*16,x+94,y+52+j*16,'#908a8a',2)
            box(x+30+j*17,y+47+j*16,10,10,'#e8e3e3',5)
    else:
        c.setStrokeColor(HexColor('#bc8c62'));c.setLineWidth(4)
        c.circle(x+56,H-y-69,23,stroke=1,fill=0)
        line(x+72,y+48,x+79,y+60,'#e8e3e3',3)
        line(x+79,y+60,x+65,y+60,'#e8e3e3',3)
lines(37,508,['Una base sperimentale per studiare il percorso dai dati alle decisioni','di trading, prima di sviluppare una soluzione completa.'],10,16)
rows=[('01','Parti dal progetto.',['Architettura e percorso di sviluppo documentati.']),('02','Esplora in locale.',['Ambiente di prova con risposte simulate del broker.']),('03','Segui l’evoluzione.',['Previsioni e backtest oggi dimostrativi; funzionalità avanzate da sviluppare.'])]
for i,(n,title,body) in enumerate(rows):
    y=561+i*46
    text(38,y,n,10,'#706c6c');text(70,y,title,11,font='B')
    lines(70,y+16,body,8.7,13)
    if i<2: line(70,y+27,558,y+27,'#e9e8e8',.6)
box(36,696,523,44,'#e8e3e3',14)
text(52,724,'Scopri il progetto nel README.',12,font='B')
line(518,724,531,711,'#242222',1.5)
line(521,711,531,711,'#242222',1.5)
line(531,711,531,721,'#242222',1.5)
c.linkURL('https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/kdp-54-flyer/README.md',(36,H-740,559,H-696),relative=0,thickness=0)
text(37,758,'Repository KDPaperClipPlaygroundRepo · README.md',8,'#494545')
lines(37,784,['Prototipo sperimentale. Previsioni senza modelli di intelligenza artificiale attivi','e backtest con risultati sintetici. Non pronto per operare con denaro reale.'],8,12,'#494545')
c.showPage();c.save()
doc=fitz.open(P/'capital-majordomo.pdf')
assert len(doc)==1
page=doc[0]
page.get_pixmap(dpi=300).save(P/'capital-majordomo.png')
page.get_pixmap(dpi=110).save('/tmp/kdp56-preview.png')
print('PDF pages:',len(doc),'PNG:',page.rect,'300 dpi')
