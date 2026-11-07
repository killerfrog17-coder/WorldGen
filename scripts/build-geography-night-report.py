"""Build the final overnight geography review from preserved in-game captures.

The manifest and embedded JPEG inputs live under docs/reports, so deleting
disposable artifacts never breaks this report or its rebuild inputs.
"""
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools/reporting/python'))
from reportlab.pdfgen import canvas
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import Paragraph, Table, TableStyle
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont


def build(manifest, output):
    data = json.loads(manifest.read_text(encoding='utf-8-sig'))
    for name, file in [('Body', 'calibri.ttf'), ('Bold', 'calibrib.ttf')]:
        pdfmetrics.registerFont(TTFont(name, str(Path('C:/Windows/Fonts') / file)))
    pdfmetrics.registerFontFamily('Body', normal='Body', bold='Bold', italic='Body', boldItalic='Bold')
    w, h = A4
    margin, gutter = 34, 14
    usable = w - margin * 2
    ink, muted = colors.HexColor('#20372F'), colors.HexColor('#54685F')
    green, line = colors.HexColor('#285E49'), colors.HexColor('#D9E2DB')
    output.parent.mkdir(parents=True, exist_ok=True)
    c = canvas.Canvas(str(output), pagesize=A4, pageCompression=1)
    c.setTitle(data['title'])
    c.setAuthor('WorldGen')
    layout = []

    def paragraph(text, x, y, width, size=10, bold=False, color=ink):
        p = Paragraph(text, ParagraphStyle('p', fontName='Bold' if bold else 'Body',
                      fontSize=size, leading=size * 1.28, textColor=color))
        _, height = p.wrap(width, h)
        if y - height < 38:
            raise ValueError(f'Page {page}: text overflows: {text[:100]}')
        p.drawOn(c, x, y - height)
        layout.append(dict(page=page, kind='text', bottom=y-height, height=height))
        return y - height

    for page, entry in enumerate(data['pages'], 1):
        if page > 1:
            c.showPage()
        c.setFillColor(green)
        c.rect(0, h-7, w, 7, fill=1, stroke=0)
        paragraph('WORLDGEN  /  GEOGRAPHY REVIEW  /  ' + data['date'], margin, h-27, usable, 8, True, green)
        paragraph(entry['title'], margin, h-48, usable, 24, True)
        y = paragraph(entry.get('subtitle', ''), margin, h-82, usable, 10, color=muted) - 18
        c.setStrokeColor(line)
        c.line(margin, 30, w-margin, 30)
        c.setFont('Body', 8)
        c.setFillColor(muted)
        c.drawString(margin, 17, 'Actual game captures • Medium vegetation unless stated')
        c.drawRightString(w-margin, 17, f'{page} / {len(data["pages"])}')
        c.bookmarkPage(f'page{page}')
        c.addOutlineEntry(entry['title'], f'page{page}', 0)
        for block in entry['blocks']:
            if 'heading' in block:
                y = paragraph(block['heading'], margin, y, usable, 11.5, True, green) - 5
            if 'text' in block:
                y = paragraph(block['text'], margin, y, usable, block.get('size', 10)) - 12
            if 'images' in block:
                images = block['images']
                cell_width = (usable - gutter * (len(images)-1)) / len(images)
                bottom = y
                for col, picture in enumerate(images):
                    path = manifest.parent / picture['file']
                    image = ImageReader(str(path))
                    iw, ih = image.getSize()
                    height = min(cell_width * ih / iw, block.get('max_height', 270))
                    width = height * iw / ih
                    x = margin + col * (cell_width + gutter)
                    if y-height < 45:
                        raise ValueError(f'Page {page}: image overflows: {path}')
                    c.drawImage(image, x+(cell_width-width)/2, y-height, width, height)
                    bottom = min(bottom, paragraph(picture['caption'], x, y-height-5, cell_width, 8.5, color=muted))
                    layout.append(dict(page=page, kind='image', source=str(path), bottom=y-height, height=height))
                y = bottom - 15
            if 'table' in block:
                table = block['table']
                rows = [[Paragraph(str(value), ParagraphStyle('cell', fontName='Bold' if i==0 else 'Body',
                         fontSize=9, leading=12, textColor=colors.white if i==0 else ink)) for value in row]
                        for i, row in enumerate([table['headers']] + table['rows'])]
                grid = Table(rows, colWidths=[usable*f for f in table.get('widths', [1/len(rows[0])]*len(rows[0]))])
                grid.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),green),
                    ('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.HexColor('#F0F5F1'),colors.white]),
                    ('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),7),
                    ('RIGHTPADDING',(0,0),(-1,-1),7),('TOPPADDING',(0,0),(-1,-1),7),
                    ('BOTTOMPADDING',(0,0),(-1,-1),7)]))
                _, height = grid.wrap(usable, h)
                if y-height < 40:
                    raise ValueError(f'Page {page}: table overflows')
                grid.drawOn(c, margin, y-height)
                y -= height+15
        if entry.get('sources'):
            paragraph('Sources: ' + ' · '.join(f'<link href="{s[1]}">{s[0]}</link>' for s in entry['sources']),
                      margin, min(y, 67), usable, 8, color=muted)
    c.save()
    output.with_suffix('.layout.json').write_text(json.dumps(layout, indent=2), encoding='utf-8')
    print(output)


if __name__ == '__main__':
    build(Path(sys.argv[1]), Path(sys.argv[2]))
