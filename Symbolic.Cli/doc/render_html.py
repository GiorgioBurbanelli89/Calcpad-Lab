#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Renderiza un HTML en Chromium headless (Playwright) y guarda:
  - <salida>.png            (lo que se VE de verdad: ecuaciones, gráficas)
  - <salida>.errores.txt    (consola + errores JS + requests fallidos)
Imprime un resumen para NO aprobar un reporte sin ver render + errores.

Uso:  python render_html.py reporte.html [salida.png]
Si falta el navegador:  python -m playwright install chromium
"""
import sys, asyncio, pathlib

async def main():
    if len(sys.argv) < 2:
        print("uso: python render_html.py reporte.html [salida.png]"); return
    html = pathlib.Path(sys.argv[1]).resolve()
    out = pathlib.Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else html.with_suffix(".png")
    errlog = out.with_suffix(".errores.txt")

    from playwright.async_api import async_playwright
    msgs = []
    async with async_playwright() as p:
        browser = await p.chromium.launch()
        page = await browser.new_page(viewport={"width": 1200, "height": 900}, device_scale_factor=2)
        page.on("console", lambda m: msgs.append(f"[{m.type}] {m.text}"))
        page.on("pageerror", lambda e: msgs.append(f"[pageerror] {e}"))
        page.on("requestfailed", lambda r: msgs.append(f"[reqfail] {r.url}"))
        await page.goto(html.as_uri(), wait_until="networkidle")
        await page.wait_for_timeout(1200)          # deja dibujar canvas/JS
        await page.screenshot(path=str(out), full_page=True)
        await browser.close()

    errs = [m for m in msgs if m.startswith(("[error", "[pageerror", "[reqfail"))]
    errlog.write_text("\n".join(msgs) or "(sin mensajes)", encoding="utf-8")
    print(f"PNG: {out}")
    print(f"Log: {errlog}  ({len(msgs)} mensajes, {len(errs)} errores)")
    if errs:
        print("--- ERRORES ---")
        for e in errs[:20]:
            print(e)
    else:
        print("Sin errores de consola/JS.")

if __name__ == "__main__":
    asyncio.run(main())
