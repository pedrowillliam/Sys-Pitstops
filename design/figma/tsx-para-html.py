"""Converte o código de referência do conector do Figma (React + Tailwind) em
um HTML autônomo, que abre direto no navegador via CDN do Tailwind.

Uso:  python tsx-para-html.py html/<arquivo>.tsx
      python tsx-para-html.py html/*.tsx

Escreve <arquivo>.html ao lado do .tsx. É só referência visual — não é o
código que vai para frontend/, que segue a stack do projeto.
"""

import glob
import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
MANIFEST = os.path.join(HERE, 'assets', 'manifest.json')


def local_assets():
    """URLs do Figma expiram em 7 dias. O manifesto aponta para as cópias
    baixadas em assets/, para o HTML continuar renderizando depois disso."""
    if not os.path.exists(MANIFEST):
        return {}
    return json.load(io.open(MANIFEST, encoding='utf-8'))

TEMPLATE = """<!doctype html>
<html lang="pt-BR">
  <head>
    <meta charset="UTF-8" />
    <title>{title}</title>
    <script src="https://cdn.tailwindcss.com"></script>
    <link
      href="https://fonts.googleapis.com/css2?family=Montserrat:wght@400;600;800&family=Montserrat+Alternates:wght@600&display=swap"
      rel="stylesheet"
    />
    <style>
      body {{ margin: 0; font-family: Montserrat, system-ui, sans-serif; }}
      .screen {{ position: relative; width: {width}px; height: {height}px; }}
    </style>
  </head>
  <body>
    <div class="screen">
{body}
    </div>
  </body>
</html>
"""

VOID = {'img', 'br', 'hr', 'input', 'meta', 'link'}


def camel_to_kebab(name):
    return re.sub(r'([A-Z])', lambda m: '-' + m.group(1).lower(), name)


def convert_style(match):
    """style={{ backgroundImage: "..." }}  ->  style="background-image: ..." """
    inner = match.group(1)
    parts = []
    for prop, value in re.findall(r'(\w+)\s*:\s*"((?:[^"\\]|\\.)*)"', inner):
        parts.append(f'{camel_to_kebab(prop)}: {value}')
    return 'style="' + '; '.join(parts) + '"' if parts else ''


def close_void_tags(markup):
    """<div ... /> is not valid HTML; only real void elements may self-close."""
    def fix(match):
        tag = match.group(1)
        attrs = match.group(2)
        if tag in VOID:
            return f'<{tag}{attrs}>'
        return f'<{tag}{attrs}></{tag}>'

    # Sem alternativa para trechos entre aspas de propósito: permitir aspas
    # deixava a regex atravessar o `>` de uma tag anterior e fechar a `<div>`
    # com `</p>`, o que aninhava os irmãos dentro dela e dobrava as posições.
    # Nenhum atributo deste código gerado contém `<` ou `>`.
    return re.sub(r'<(\w+)([^<>]*?)\s*/>', fix, markup)


def convert(path):
    source = io.open(path, encoding='utf-8').read()

    images = dict(re.findall(r'const\s+(\w+)\s*=\s*"([^"]+)"\s*;', source))

    start = source.find('return (')
    end = source.rfind(');')
    if start == -1 or end == -1:
        raise SystemExit(f'{path}: não achei o corpo do componente.')

    body = source[start + len('return ('):end]

    # Code Connect wrappers are notes to the implementer, not markup.
    body = re.sub(
        r'<CodeConnectSnippet[^>]*>.*?</CodeConnectSnippet>',
        '<!-- componente do design system -->',
        body,
        flags=re.S,
    )
    body = re.sub(r'\{/\*.*?\*/\}', '', body, flags=re.S)

    body = re.sub(r'style=\{\{(.*?)\}\}', convert_style, body, flags=re.S)

    saved = local_assets()
    for name, url in images.items():
        target_url = f'../assets/{saved[url]}' if url in saved else url
        body = body.replace('src={%s}' % name, f'src="{target_url}"')
    for url, filename in saved.items():
        body = body.replace(url, f'../assets/{filename}')
    body = body.replace('className=', 'class=')

    # Dois consertos no código que o conector devolve:
    #
    # 1. `absolute contents` — se o `display: contents` valer, o `absolute` não
    #    faz nada; se não valer, o grupo vira uma caixa posicionada e os filhos
    #    passam a somar o deslocamento duas vezes. Deixar só `contents` é
    #    correto nos dois casos.
    # 2. `font-['Montserrat:SemiBold']` não é uma família de fonte válida — o
    #    peso vem junto do nome. Removendo, sobra a Montserrat do <body> mais
    #    as classes de peso, que o próprio código já traz.
    body = re.sub(r'\babsolute contents\b', 'contents', body)
    body = re.sub(r"font-\['[^']*'\]\s*", '', body)

    body = close_void_tags(body)

    size = re.search(r'w-\[(\d+(?:\.\d+)?)px\].*?h-\[(\d+(?:\.\d+)?)px\]', source)
    width, height = (size.group(1), size.group(2)) if size else ('1728', '1123')
    # The outer frame is usually the first sized element; fall back to A4-ish.
    first = re.search(r'h-\[(\d+(?:\.\d+)?)px\][^"]*w-\[(\d+(?:\.\d+)?)px\]', source)
    if first:
        height, width = first.group(1), first.group(2)

    title = os.path.splitext(os.path.basename(path))[0]
    target = os.path.splitext(path)[0] + '.html'

    io.open(target, 'w', encoding='utf-8').write(
        TEMPLATE.format(title=title, width=width, height=height, body=body)
    )
    print(f'{target}  ({width}x{height})')


paths = []
for pattern in sys.argv[1:]:
    paths.extend(glob.glob(pattern))

if not paths:
    raise SystemExit('uso: python tsx-para-html.py html/*.tsx')

for path in paths:
    convert(path)
