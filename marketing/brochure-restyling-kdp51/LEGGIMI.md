# Brochure Capital Majordomo — revisione tipografica

PDF digitale A4 di due pagine e sorgente PowerPoint editabile.

Titoli Poppins SemiBold, sezioni Poppins Medium e corpo Lora: contrasto sans/serif, fondo avorio #FAF9F5, testo #141413, accento terracotta. Il terracotta del testo è scurito a #A6472D per la leggibilità. Riferimento visivo: https://github.com/anthropics/skills/blob/main/skills/brand-guidelines/SKILL.md (consultato 28 ottobre 2026). Interpretazione per Capital Majordomo, senza loghi o grafica proprietaria Anthropic.

Contenuti identici alla brochure consegnata in KDP-47: confronto automatico del testo PDF ignorando solo gli spazi e le interruzioni di riga. Formato e avvertenze conservati. Questa revisione riguarda solo la brochure.

## Modifica e rigenerazione

Installare i font della cartella font prima di aprire brochure.pptx. I font sono distribuiti con licenza SIL Open Font License, inclusa per ogni famiglia; il PDF incorpora i font. Il PPTX contiene testo e forme editabili, senza font incorporati.

Per rigenerare: Python 3 con reportlab, python-pptx, pymupdf e pillow; eseguire `python3 genera.py` dalla cartella estratta. Il copy sorgente è incluso in copy-mario.md, preservato integralmente. Le immagini pagina-1.png e pagina-2.png sono anteprime.

## Verifica e stato

Controllati visivamente entrambi i PDF, verificati testo entro pagina e conteggio di due pagine/due slide. PPTX riaperto con python-pptx; resa nel programma Microsoft PowerPoint non testata. PDF e PPTX prodotti dal medesimo layout.

File per uso digitale RGB, non esecutivi CMYK con abbondanze. Pubblicazione e stampa restano soggette ad approvazione. Nessuna pubblicazione o invio in stampa effettuati.
