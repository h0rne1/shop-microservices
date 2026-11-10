"""
Генератор C4-схемы интернет-магазина.

Создаёт:
  docs/architecture.drawio  — редактируемая схема (3 страницы: Context, Container, Component)
  docs/architecture.html    — та же схема в SVG (используется для экспорта в PNG/PDF)

Экспорт (macOS, Google Chrome):
  python3 docs/gen_architecture.py
  "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless \
      --screenshot=docs/architecture.png --window-size=1100,3010 docs/architecture.html
"""
from pathlib import Path
from xml.sax.saxutils import escape

DOCS = Path(__file__).parent

# Цвета нотации C4
STYLE = {
    "person":    dict(fill="#08427B", stroke="#073B6F", font="#FFFFFF"),
    "system":    dict(fill="#1168BD", stroke="#0B4884", font="#FFFFFF"),
    "container": dict(fill="#438DD5", stroke="#3C7FC0", font="#FFFFFF"),
    "db":        dict(fill="#438DD5", stroke="#3C7FC0", font="#FFFFFF"),
    "component": dict(fill="#85BBF0", stroke="#78A8D8", font="#000000"),
    "ext":       dict(fill="#999999", stroke="#8A8A8A", font="#FFFFFF"),
    "extdb":     dict(fill="#999999", stroke="#8A8A8A", font="#FFFFFF"),
}


class E:
    def __init__(self, id, kind, name, tech, desc, x, y, w=240, h=130):
        self.id, self.kind, self.name, self.tech, self.desc = id, kind, name, tech, desc
        self.x, self.y, self.w, self.h = x, y, w, h

    @property
    def cx(self): return self.x + self.w / 2

    @property
    def cy(self): return self.y + self.h / 2


class Page:
    def __init__(self, name, title, width, height, elements, boundaries, edges):
        self.name, self.title, self.width, self.height = name, title, width, height
        self.elements = {e.id: e for e in elements}
        self.boundaries = boundaries      # (label, x, y, w, h)
        self.edges = edges                # (from, to, label)


# ---------------------------------------------------------------- модель
customer = lambda x, y: E("customer", "person", "Покупатель", "Person",
                          "Просматривает каталог и оформляет заказы", x, y, 230, 140)
manager = lambda x, y: E("manager", "person", "Менеджер магазина", "Person",
                         "Добавляет товары и следит за остатками", x, y, 230, 140)

context = Page(
    "1. System Context", "Уровень 1 — System Context: Интернет-магазин", 1100, 600,
    [
        customer(130, 70), manager(740, 70),
        E("shop", "system", "Интернет-магазин", "Software System",
          "Каталог товаров, оформление заказов с резервированием на складе и оплатой", 330, 360, 440, 160),
    ],
    [],
    [
        ("customer", "shop", "Смотрит каталог, оформляет заказы\n[HTTP/JSON]"),
        ("manager", "shop", "Управляет товарами и остатками\n[HTTP/JSON]"),
    ],
)

container = Page(
    "2. Container", "Уровень 2 — Container: Интернет-магазин (docker compose)", 1100, 880,
    [
        customer(140, 60), manager(730, 60),
        E("order", "container", "order-service", "Container: .NET 9, ASP.NET Core Minimal API, :5002",
          "Оформление заказов; оркестрация резерва и оплаты", 80, 330, 290, 150),
        E("catalog", "container", "catalog-service", "Container: .NET 9, ASP.NET Core Minimal API, :5001",
          "Каталог товаров, остатки, резерв/возврат", 730, 330, 290, 150),
        E("payment", "container", "payment-service", "Container: .NET 9, ASP.NET Core Minimal API, :5003",
          "Проведение платежей, лимит 100 000", 80, 600, 290, 150),
        E("db", "db", "PostgreSQL (db)", "Container: postgres:16, volume db_data",
          "Базы catalog_db, orders_db, payments_db", 730, 600, 290, 150),
    ],
    [("Интернет-магазин [Software System]", 40, 270, 1020, 560)],
    [
        ("customer", "order", "Оформляет заказ\n[HTTP/JSON]"),
        ("customer", "catalog", "Смотрит каталог\n[HTTP/JSON]"),
        ("manager", "catalog", "Добавляет товары\n[HTTP/JSON]"),
        ("order", "catalog", "Резерв / возврат товара\n[HTTP/JSON]"),
        ("order", "payment", "Оплата заказа\n[HTTP/JSON]"),
        ("catalog", "db", "catalog_db\n[EF Core / Npgsql]"),
        ("payment", "db", "payments_db\n[EF Core / Npgsql]"),
        ("order", "db", "orders_db\n[EF Core / Npgsql]"),
    ],
)

component = Page(
    "3. Component (order-service)", "Уровень 3 — Component: order-service", 1100, 1030,
    [
        customer(70, 50),
        E("api", "component", "Orders API", "Component: Minimal API endpoints (Program.cs)",
          "POST/GET /api/orders, /health", 70, 290, 250, 130),
        E("proc", "component", "OrderProcessor", "Component: сервис (Services/OrderProcessor.cs)",
          "Резерв → оплата → компенсация", 430, 290, 250, 130),
        E("dbctx", "component", "OrderDbContext", "Component: EF Core DbContext (Data/)",
          "Таблицы orders, order_items", 70, 530, 250, 130),
        E("cclient", "component", "CatalogClient", "Component: typed HttpClient (Clients/)",
          "Вызовы /reserve и /release", 430, 530, 250, 130),
        E("pclient", "component", "PaymentClient", "Component: typed HttpClient (Clients/)",
          "Вызов POST /api/payments", 790, 530, 250, 130),
        E("ordersdb", "extdb", "orders_db", "Container: PostgreSQL 16",
          "Заказы и позиции", 70, 850, 250, 130),
        E("catalog", "ext", "catalog-service", "Container: .NET 9",
          "Товары и остатки", 430, 850, 250, 130),
        E("payment", "ext", "payment-service", "Container: .NET 9",
          "Платежи", 790, 850, 250, 130),
    ],
    [("order-service [Container]", 40, 250, 1030, 450)],
    [
        ("customer", "api", "POST /api/orders\n[HTTP/JSON]"),
        ("api", "proc", "PlaceOrderAsync()"),
        ("api", "dbctx", "Чтение заказов"),
        ("proc", "dbctx", "Сохраняет заказ"),
        ("proc", "cclient", "Резерв / возврат"),
        ("proc", "pclient", "Оплата"),
        ("dbctx", "ordersdb", "SQL [Npgsql]"),
        ("cclient", "catalog", "HTTP/JSON"),
        ("pclient", "payment", "HTTP/JSON"),
    ],
)

PAGES = [context, container, component]


# ---------------------------------------------------------------- геометрия
def clip(e, tx, ty):
    """Точка на границе прямоугольника e по направлению к (tx, ty)."""
    dx, dy = tx - e.cx, ty - e.cy
    if dx == 0 and dy == 0:
        return e.cx, e.cy
    sx = (e.w / 2) / abs(dx) if dx else float("inf")
    sy = (e.h / 2) / abs(dy) if dy else float("inf")
    s = min(sx, sy)
    return e.cx + dx * s, e.cy + dy * s


# ---------------------------------------------------------------- draw.io
def drawio_label(e):
    return (f"<b>{escape(e.name)}</b><br><font style='font-size:10px'>[{escape(e.tech)}]</font>"
            f"<br><br>{escape(e.desc)}")


def drawio_style(e):
    s = STYLE[e.kind]
    base = f"fillColor={s['fill']};strokeColor={s['stroke']};fontColor={s['font']};whiteSpace=wrap;html=1;fontSize=12;"
    if e.kind == "person":
        return "shape=mxgraph.c4.person2;labelPosition=center;verticalLabelPosition=middle;align=center;verticalAlign=bottom;spacingBottom=8;" + base
    if e.kind in ("db", "extdb"):
        return "shape=cylinder3;boundedLbl=1;backgroundOutline=1;size=12;" + base
    return "rounded=1;arcSize=8;" + base


def build_drawio():
    out = ['<?xml version="1.0" encoding="UTF-8"?>',
           '<mxfile host="drawio" type="device">']
    for pi, p in enumerate(PAGES):
        out.append(f'  <diagram id="page{pi}" name="{escape(p.name)}">')
        out.append(f'    <mxGraphModel dx="1200" dy="900" grid="1" gridSize="10" guides="1" page="1" '
                   f'pageWidth="{p.width}" pageHeight="{p.height}" math="0" shadow="0">')
        out.append('      <root>\n        <mxCell id="0"/>\n        <mxCell id="1" parent="0"/>')
        out.append(f'        <mxCell id="title" value="{escape(p.title)}" style="text;html=1;fontSize=18;fontStyle=1;align=left;" '
                   f'vertex="1" parent="1"><mxGeometry x="20" y="5" width="900" height="30" as="geometry"/></mxCell>')
        for bi, (label, x, y, w, h) in enumerate(p.boundaries):
            out.append(f'        <mxCell id="b{bi}" value="{escape(label)}" '
                       'style="rounded=0;dashed=1;dashPattern=8 4;fillColor=none;strokeColor=#666666;fontColor=#333333;'
                       'verticalAlign=bottom;align=left;spacingLeft=10;spacingBottom=6;fontStyle=1;html=1;" '
                       f'vertex="1" parent="1"><mxGeometry x="{x}" y="{y}" width="{w}" height="{h}" as="geometry"/></mxCell>')
        for e in p.elements.values():
            out.append(f'        <mxCell id="{e.id}" value="{escape(drawio_label(e), {chr(34): "&quot;"})}" '
                       f'style="{drawio_style(e)}" vertex="1" parent="1">'
                       f'<mxGeometry x="{e.x}" y="{e.y}" width="{e.w}" height="{e.h}" as="geometry"/></mxCell>')
        for ei, (a, b, label) in enumerate(p.edges):
            lbl = escape(label.replace("\n", "<br>"), {chr(34): "&quot;"})
            out.append(f'        <mxCell id="e{ei}" value="{lbl}" '
                       'style="endArrow=block;endFill=1;html=1;rounded=0;strokeColor=#707070;fontSize=11;'
                       'labelBackgroundColor=#FFFFFF;fontColor=#404040;" '
                       f'edge="1" parent="1" source="{a}" target="{b}"><mxGeometry relative="1" as="geometry"/></mxCell>')
        out.append('      </root>\n    </mxGraphModel>\n  </diagram>')
    out.append('</mxfile>')
    (DOCS / "architecture.drawio").write_text("\n".join(out), encoding="utf-8")


# ---------------------------------------------------------------- SVG
def wrap(text, width_chars):
    words, lines, cur = text.split(), [], ""
    for w in words:
        if len(cur) + len(w) + 1 > width_chars and cur:
            lines.append(cur)
            cur = w
        else:
            cur = f"{cur} {w}".strip()
    if cur:
        lines.append(cur)
    return lines


def svg_element(e):
    s = STYLE[e.kind]
    parts = []
    top = e.y
    if e.kind == "person":
        r = 26
        parts.append(f'<circle cx="{e.cx}" cy="{e.y + r}" r="{r}" fill="{s["fill"]}" stroke="{s["stroke"]}"/>')
        top = e.y + 2 * r - 6
        parts.append(f'<rect x="{e.x}" y="{top}" width="{e.w}" height="{e.y + e.h - top}" rx="28" '
                     f'fill="{s["fill"]}" stroke="{s["stroke"]}"/>')
    elif e.kind in ("db", "extdb"):
        ry = 12
        parts.append(f'<path d="M{e.x},{e.y + ry} v{e.h - 2 * ry} a{e.w / 2},{ry} 0 0 0 {e.w},0 v{-(e.h - 2 * ry)} '
                     f'a{e.w / 2},{ry} 0 0 0 {-e.w},0 Z" fill="{s["fill"]}" stroke="{s["stroke"]}"/>')
        parts.append(f'<path d="M{e.x},{e.y + ry} a{e.w / 2},{ry} 0 0 0 {e.w},0" fill="none" stroke="{s["stroke"]}"/>')
        top = e.y + 2 * ry
    else:
        parts.append(f'<rect x="{e.x}" y="{e.y}" width="{e.w}" height="{e.h}" rx="10" '
                     f'fill="{s["fill"]}" stroke="{s["stroke"]}"/>')

    lines = [(e.name, 15, "bold")]
    lines += [(l, 10.5, "tech") for l in wrap(f"[{e.tech}]", int(e.w / 6.2))]
    lines += [("", 6, "")]
    lines += [(l, 12, "") for l in wrap(e.desc, int(e.w / 6.8))]
    total = sum(sz * 1.25 for _, sz, _ in lines)
    y = top + (e.y + e.h - top - total) / 2
    for text, sz, kind in lines:
        y += sz * 1.25
        if not text:
            continue
        weight = ' font-weight="700"' if kind == "bold" else ""
        op = ' opacity="0.85"' if kind == "tech" else ""
        parts.append(f'<text x="{e.cx}" y="{y - sz * 0.25:.1f}" font-size="{sz}"{weight}{op} '
                     f'fill="{s["font"]}" text-anchor="middle">{escape(text)}</text>')
    return "\n".join(parts)


def svg_page(p):
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{p.width}" height="{p.height}" '
           f'viewBox="0 0 {p.width} {p.height}" font-family="Helvetica, Arial, sans-serif">',
           '<defs><marker id="arr" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="9" markerHeight="9" '
           'orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10 z" fill="#707070"/></marker></defs>',
           f'<rect width="{p.width}" height="{p.height}" fill="#FFFFFF"/>',
           f'<text x="20" y="32" font-size="20" font-weight="700" fill="#222">{escape(p.title)}</text>']
    for label, x, y, w, h in p.boundaries:
        out.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="none" stroke="#666" '
                   f'stroke-dasharray="8 4" stroke-width="1.5"/>')
        out.append(f'<text x="{x + 12}" y="{y + h - 12}" font-size="13" font-weight="700" fill="#333">{escape(label)}</text>')
    for e in p.elements.values():
        out.append(svg_element(e))
    for a, b, label in p.edges:
        ea, eb = p.elements[a], p.elements[b]
        x1, y1 = clip(ea, eb.cx, eb.cy)
        x2, y2 = clip(eb, ea.cx, ea.cy)
        out.append(f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="#707070" '
                   f'stroke-width="1.6" marker-end="url(#arr)"/>')
        mx, my = (x1 + x2) / 2, (y1 + y2) / 2
        rows = label.split("\n")
        bw = max(len(r) for r in rows) * 6.4 + 12
        bh = len(rows) * 15 + 6
        out.append(f'<rect x="{mx - bw / 2:.1f}" y="{my - bh / 2:.1f}" width="{bw:.1f}" height="{bh}" '
                   f'fill="#FFFFFF" opacity="0.92" rx="3"/>')
        for i, r in enumerate(rows):
            ty = my - bh / 2 + 15 + i * 15
            out.append(f'<text x="{mx:.1f}" y="{ty:.1f}" font-size="11.5" fill="#404040" '
                       f'text-anchor="middle">{escape(r)}</text>')
    # легенда
    lx, ly = p.width - 360, p.height - 30
    legend = {
        "context": [("person", "Person"), ("system", "Software System")],
        "container": [("person", "Person"), ("container", "Container")],
        "component": [("person", "Person"), ("component", "Component"), ("ext", "Другой контейнер")],
    }[{id(context): "context", id(container): "container"}.get(id(p), "component")]
    for i, (k, t) in enumerate(legend):
        out.append(f'<rect x="{lx + i * 110}" y="{ly}" width="14" height="14" fill="{STYLE[k]["fill"]}"/>'
                   f'<text x="{lx + i * 110 + 19}" y="{ly + 12}" font-size="11" fill="#444">{t}</text>')
    out.append("</svg>")
    return "\n".join(out)


def build_html():
    pages = "\n".join(f'<div class="page">{svg_page(p)}</div>' for p in PAGES)
    html = f"""<!doctype html><html><head><meta charset="utf-8"><title>C4 — Интернет-магазин</title>
<style>
  html, body {{ margin: 0; background: #fff; }}
  .page {{ width: 1100px; break-after: page; }}
  .page svg {{ display: block; }}
  @page {{ size: 1100px 1030px; margin: 0; }}
</style></head><body>
{pages}
</body></html>"""
    (DOCS / "architecture.html").write_text(html, encoding="utf-8")


if __name__ == "__main__":
    build_drawio()
    build_html()
    print("total height:", sum(p.height for p in PAGES))
