# Flyer Capital-Majordomo

Flyer italiano A4 verticale, una pagina, destinato alla condivisione digitale e alla verifica finale del CEO. Non costituisce autorizzazione alla diffusione pubblicitaria o alla stampa.

## File

- `capital-majordomo.pdf`: PDF vettoriale con font incorporati e CTA cliccabile al README del branch di consegna.
- `capital-majordomo.png`: export dello stesso PDF a 300 dpi (2481 × 3508 px).
- `generate.py`: sorgente modificabile, con testi e geometrie vettoriali.
- `assets/`: font Poppins Medium e SemiBold, con licenza SIL Open Font License.
- `copy-definitivo.md`: copy utilizzato integralmente, con soli a capo di impaginazione.
- `copy-editoriale.md`: documento del copywriter e note di verifica; fonte Paperclip KDP-55, revisione `606f6bef-e2c6-4476-90e6-2b07cab501aa`.

## Rigenerazione

Python 3.13; dipendenze: `reportlab==5.1.0`, `pymupdf==2.0.0` (la distribuzione installata dichiara 2.0). Installare con `python3 -m pip install -r requirements.txt`, quindi eseguire `python3 generate.py` dalla cartella del flyer. I font sono locali: non occorrono asset remoti. Il PNG viene rasterizzato dal PDF per garantire corrispondenza. Il sorgente produce anche una preview temporanea in `/tmp/kdp56-preview.png`.

Formato 210 × 297 mm, colori RGB per schermo, margine minimo 12 mm; PNG a 300 dpi. Non è un esecutivo tipografico CMYK/PDF-X con abbondanze: per una tiratura serve un export dedicato secondo le specifiche della tipografia e previa approvazione.

## Riferimento grafico osservato il 28 ottobre 2026

Consultato direttamente [Capital.com](https://capital.com/), con redirect alla [home internazionale](https://capital.com/en-int). Osservati HTML, stylesheet e [visual hero](https://img.capital.com/imgs/blocks/640xx0x1/hero_interface_en-INT-USD.webp). Il browser locale non disponeva delle librerie di sistema necessarie: analisi del CSS e ispezione visiva diretta dell'immagine hero scaricata, senza attribuire al sito un aspetto ricostruito da memoria.

CSS di riferimento: [stylesheet attuale](https://static.capital.com/capital-v1-0-2/apps/capital/_next/static/chunks/3caxznoa1mx09.css). Token osservati: base `#050202`, `#141212`, `#242222`, `#e8e3e3`, `#f4f3f3`; accento `--primary: #bc8c62`. Le famiglie dichiarate dal sito sono Geigy LL e SF Pro: non distribuite né utilizzate qui. Poppins, disponibile con OFL, offre una gerarchia geometrica indipendente.

Scelte: titolo ampio, spazi bianchi, pannello quasi nero, schede arrotondate sovrapposte e dettagli neutri caldi. Il visual originale collega tre fasi astratte ai tre punti editoriali; non mostra una schermata reale, rendimenti o metriche di investimento. Grafica vettoriale realizzata nel sorgente: nessuna immagine generativa necessaria. Nessun logo o asset Capital.com è incluso nei deliverable. Il nome di progetto è una semplice intestazione tipografica; il flyer non rappresenta una partnership o un'approvazione di Capital.com.

## Fatti e verifiche

Letture di riferimento: `README.md`, `docs/capital-majordomo/README.md` e documento copy di KDP-55. Lo stato skeleton/stub è esplicito nel testo; architettura e roadmap non sono presentate come prodotto completo. Nessun contatto o rendimento inventato. CTA al README esistente del repository configurato.

Verifica: PDF di una pagina aperto e rasterizzato; PNG aperto e ispezionato visivamente, compresi titolo, tre punti, CTA e nota di trasparenza. Nessun taglio, sovrapposizione o asset mancante. Controllo del testo estratto, font incorporati e corrispondenza pixel tra PNG e nuova rasterizzazione del PDF.
