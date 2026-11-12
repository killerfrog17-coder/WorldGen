"""Build the two-page visual handoff from the original, unmodified captures.

Run only after gameplay performance measurements have finished. The coordinator
supplies final status/metrics through --status and --fps-note, then inspects both
PDF pages before opening the report. Image details are PDF clips, not edited PNGs.
"""

from __future__ import annotations

import argparse
from html import escape
from pathlib import Path
import struct

from reportlab.lib import colors
from reportlab.lib.pagesizes import landscape, letter
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas
from reportlab.platypus import Paragraph


HERE = Path(__file__).resolve().parent
PAGE_W, PAGE_H = landscape(letter)
MARGIN, GUTTER = 34, 16
COLUMN = (PAGE_W - MARGIN * 2 - GUTTER) / 2
RIGHT = MARGIN + COLUMN + GUTTER
INK = colors.HexColor("#202F34")
MUTED = colors.HexColor("#53646A")
ACCENT = colors.HexColor("#43776F")
PAPER = colors.HexColor("#F6F5F0")
LINE = colors.HexColor("#C9D2CF")
PANEL = colors.HexColor("#E8EEEA")


def fonts() -> tuple[str, str]:
    regular = Path("C:/Windows/Fonts/segoeui.ttf")
    bold = Path("C:/Windows/Fonts/seguisb.ttf")
    if regular.is_file() and bold.is_file():
        pdfmetrics.registerFont(TTFont("ReportBody", str(regular)))
        pdfmetrics.registerFont(TTFont("ReportBold", str(bold)))
        return "ReportBody", "ReportBold"
    return "Helvetica", "Helvetica-Bold"


def png_size(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    if header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"Expected an original PNG capture: {path}")
    return struct.unpack(">II", header[16:24])


def paragraph(c: canvas.Canvas, text: str, x: float, top: float,
              width: float, font: str, size: float = 10,
              leading: float = 13, max_height: float = 26,
              colour=INK) -> None:
    item = Paragraph(escape(text), ParagraphStyle(
        "report", fontName=font, fontSize=size, leading=leading,
        textColor=colour, spaceBefore=0, spaceAfter=0))
    _, height = item.wrap(width, max_height)
    if height > max_height + 0.1:
        raise ValueError(f"Report text does not fit; shorten the supplied note: {text}")
    item.drawOn(c, x, top - height)


def screenshot(c: canvas.Canvas, image: Path, x: float, y: float) -> None:
    width, height = png_size(image)
    drawn_height = COLUMN * height / width
    c.drawImage(str(image), x, y, width=COLUMN, height=drawn_height)
    c.setStrokeColor(LINE)
    c.setLineWidth(0.6)
    c.rect(x, y, COLUMN, drawn_height, fill=0, stroke=1)


def detail(c: canvas.Canvas, image: Path, crop: tuple[int, int, int, int],
           x: float, top: float) -> None:
    """Clip and enlarge original pixels; crop coordinates start at PNG top-left."""
    iw, ih = png_size(image)
    left, upper, width, height = crop
    if left < 0 or upper < 0 or left + width > iw or upper + height > ih:
        raise ValueError(f"Detail crop escapes the original capture: {image}")
    scale = COLUMN / width
    drawn_height = height * scale
    bottom = top - drawn_height
    c.saveState()
    clip = c.beginPath()
    clip.rect(x, bottom, COLUMN, drawn_height)
    c.clipPath(clip, stroke=0, fill=0)
    c.drawImage(str(image), x - left * scale,
                bottom - (ih - upper - height) * scale,
                width=iw * scale, height=ih * scale)
    c.restoreState()
    c.setStrokeColor(LINE)
    c.setLineWidth(0.6)
    c.rect(x, bottom, COLUMN, drawn_height, fill=0, stroke=1)


def page_header(c: canvas.Canvas, title: str, subtitle: str,
                body: str, bold: str, date: str, number: int) -> None:
    c.setFillColor(PAPER)
    c.rect(0, 0, PAGE_W, PAGE_H, fill=1, stroke=0)
    c.setFillColor(ACCENT)
    c.rect(0, PAGE_H - 5, PAGE_W, 5, fill=1, stroke=0)
    c.setFont(bold, 9)
    c.drawString(MARGIN, 579, "WORLDGEN  /  TERRAIN FEEDBACK")
    c.setFillColor(MUTED)
    c.setFont(body, 9)
    c.drawRightString(PAGE_W - MARGIN, 579, date)
    c.setFillColor(INK)
    c.setFont(bold, 25)
    c.drawString(MARGIN, 548, title)
    paragraph(c, subtitle, MARGIN, 532, PAGE_W - 2 * MARGIN,
              body, size=10.2, leading=13.2, max_height=28, colour=MUTED)
    c.setFont(bold, 10)
    c.setFillColor(MUTED)
    c.drawString(MARGIN, 489, "BEFORE")
    c.setFillColor(ACCENT)
    c.drawString(RIGHT, 489, "AFTER")
    c.setFillColor(MUTED)
    c.setFont(body, 8)
    c.drawString(MARGIN, 12, "Original game captures; matched world, camera, daylight and terrain preset.")
    c.drawRightString(PAGE_W - MARGIN, 12, f"{number} / 2")


def status_panel(c: canvas.Canvas, status: str, fps_note: str,
                 body: str, bold: str) -> None:
    c.setFillColor(PANEL)
    c.roundRect(MARGIN, 26, PAGE_W - 2 * MARGIN, 36, 5, fill=1, stroke=0)
    paragraph(c, status, MARGIN + 10, 55, PAGE_W - 2 * MARGIN - 20,
              bold, size=9.1, leading=11, max_height=11)
    paragraph(c, fps_note, MARGIN + 10, 43, PAGE_W - 2 * MARGIN - 20,
              body, size=8.8, leading=10.5, max_height=11, colour=MUTED)


def build(args: argparse.Namespace) -> Path:
    body, bold = fonts()
    images = {name: HERE / f"{name}.png" for name in (
        "ground-before", "ground-after", "canyon-before", "canyon-after")}
    for image in images.values():
        png_size(image)
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    c = canvas.Canvas(str(output), pagesize=(PAGE_W, PAGE_H), pageCompression=1)
    c.setTitle("WorldGen - Terrain Feedback Repairs")
    c.setAuthor("WorldGen")
    c.setSubject("Matched native terrain captures, representative visual repairs and qualified performance status")

    page_header(c, "Ground cover without green islands",
                "Partial vegetation cover keeps soil visible instead of forming sharp, flat green ovals.",
                body, bold, args.date, 1)
    screenshot(c, images["ground-before"], MARGIN, 278)
    screenshot(c, images["ground-after"], RIGHT, 278)
    c.setFillColor(MUTED)
    c.setFont(body, 9)
    c.drawString(MARGIN, 260, "GROUND DETAIL  /  same original pixels, enlarged")
    for image, x in ((images["ground-before"], MARGIN), (images["ground-after"], RIGHT)):
        detail(c, image, (0, 450, 1000, 414), x, 249)
    paragraph(c, "The cover blend now uses its physical area fraction; the existing soil and vegetation textures remain.",
              MARGIN, 91, PAGE_W - 2 * MARGIN, body, size=9.7, leading=12, max_height=12)
    paragraph(c, "Tree visibility was inspected at 40, 80, 140 and 250 m using one generated tree; trees remain visible beyond 100 m.",
              MARGIN, 76, PAGE_W - 2 * MARGIN, body, size=9.2, leading=11, max_height=11, colour=MUTED)
    status_panel(c, args.status, args.fps_note, body, bold)
    c.showPage()

    page_header(c, "Cliff lighting without the comb pattern",
                "The inspected canyon walls keep their original geometry and collision surface; lighting normals are filtered in both directions.",
                body, bold, args.date, 2)
    screenshot(c, images["canyon-before"], MARGIN, 278)
    screenshot(c, images["canyon-after"], RIGHT, 278)
    c.setFillColor(MUTED)
    c.setFont(body, 9)
    c.drawString(MARGIN, 260, "WALL DETAIL  /  same original pixels, enlarged")
    for image, x in ((images["canyon-before"], MARGIN), (images["canyon-after"], RIGHT)):
        detail(c, image, (1050, 200, 486, 206), x, 249)
    paragraph(c, "A native channel-edge case reduced an 11.35 cm raw mesh mismatch to 0.015 mm after the renderer's edge fitting.",
              MARGIN, 91, PAGE_W - 2 * MARGIN, body, size=9.7, leading=12, max_height=12)
    paragraph(c, "Collision contact matched the fitted surface. These are representative inspected locations; the world-wide 100 FPS floor is not certified.",
              MARGIN, 76, PAGE_W - 2 * MARGIN, body, size=9.2, leading=11, max_height=11, colour=MUTED)
    status_panel(c, args.status, args.fps_note, body, bold)
    c.save()
    return output


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--status", default="Visual repairs checked; playable handoff pending.")
    parser.add_argument("--fps-note", default="100 FPS floor remains unmet. No overall FPS gain is claimed.")
    parser.add_argument("--date", default="11 Nov 2026")
    parser.add_argument("--output", type=Path, default=HERE / "WorldGen - Terrain Feedback Repairs.pdf")
    print(build(parser.parse_args()))


if __name__ == "__main__":
    main()
