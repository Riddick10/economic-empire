Font-Ordner fuer Economic Empire
=================================

Verwendete Schriftart: VCR_OSD_MONO.ttf (VCR OSD Mono, Retro-Pixel-Look)

Die Schriftart unterstuetzt alle deutschen Sonderzeichen:
Ä Ö Ü ä ö ü ß (verifiziert - die Glyphen sind im Font enthalten und
werden in Program.Font.cs beim Laden explizit angefordert).

Beim Spielstart loggt der Font-Loader eine Diagnose:
"[Font] Geladen: ... (Umlaute OK)" - falls Glyphen fehlen sollten
(z.B. nach einem Font-Tausch), erscheint stattdessen eine Warnung
mit den fehlenden Zeichen.

Falls die Schriftart-Datei fehlt, wird automatisch der Raylib-Standard-
Font verwendet (dieser hat KEINE Umlaute).

---
DejaVuSans-Bold.ttf  -  fuer die Laendernamen auf der Karte (klare, glatte Schrift).
DejaVu Fonts sind frei lizenziert (DejaVu-Lizenz, auf Bitstream Vera basierend),
frei nutzbar und weitergebbar, auch kommerziell.
