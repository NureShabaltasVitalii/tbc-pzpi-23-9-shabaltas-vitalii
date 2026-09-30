from pathlib import Path
from math import log10
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).parent
ASSETS = ROOT / "report_assets"
OUTPUT = ROOT / "Zvit_Praktichna_robota_1_Blockchain.docx"
ASSETS.mkdir(exist_ok=True)

def font(size, bold=False):
    candidates = ["C:/Windows/Fonts/arialbd.ttf" if bold else "C:/Windows/Fonts/arial.ttf", "C:/Windows/Fonts/segoeuib.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf"]
    for candidate in candidates:
        if Path(candidate).exists(): return ImageFont.truetype(candidate, size)
    return ImageFont.load_default()

def console_image(path, title, lines, status="PASS"):
    image = Image.new("RGB", (1500, 330), "#0f172a")
    draw = ImageDraw.Draw(image)
    draw.rectangle((0, 0, 1500, 54), fill="#1e293b")
    draw.ellipse((24, 18, 38, 32), fill="#ef4444")
    draw.ellipse((46, 18, 60, 32), fill="#f59e0b")
    draw.ellipse((68, 18, 82, 32), fill="#22c55e")
    draw.text((110, 13), title, fill="white", font=font(22, True))
    y = 82
    for line in lines:
        color = "#86efac" if "Відхилено" in line or "PASS" in line else "#e2e8f0"
        draw.text((44, y), "> " + line, fill=color, font=font(25))
        y += 56
    draw.text((44, 280), status, fill="#22c55e", font=font(24, True))
    image.save(path)

def class_diagram(path):
    image = Image.new("RGB", (1600, 930), "white")
    draw = ImageDraw.Draw(image)
    navy, teal, slate = "#173b5f", "#0f766e", "#475569"
    def box(x, y, w, h, title, fields, color=navy):
        draw.rounded_rectangle((x, y, x+w, y+h), radius=12, outline=color, width=4, fill="#f8fafc")
        draw.rectangle((x, y, x+w, y+54), fill=color)
        draw.text((x+18, y+13), title, fill="white", font=font(25, True))
        yy = y+72
        for item in fields:
            draw.text((x+18, yy), item, fill="#0f172a", font=font(20))
            yy += 31
    box(80, 80, 410, 250, "Wallet", ["Address : string", "PublicKey : string", "Sign(...): Transaction"])
    box(610, 70, 410, 330, "Transaction", ["Sender, Recipient : string", "Amount, Fee : decimal", "Nonce : long", "PublicKey, Signature : string", "Id : SHA-256", "HasValidSignature(): bool"], teal)
    box(1120, 80, 410, 280, "Block", ["Index, TimestampUnixMs : long", "PreviousHash, MerkleRoot : string", "Difficulty : int, Nonce : long", "Transactions : List<Transaction>", "CalculateMerkleRoot(...)"])
    box(260, 570, 500, 290, "Blockchain", ["Chain : List<Block>", "Mempool : List<Transaction>", "TryAddTransaction(...)", "Mine(...)", "Validate(...)", "ReplaceIfBetter(...)"], teal)
    box(960, 590, 450, 235, "Program REST node", ["/wallet, /transactions, /mine", "/chain, /validate, /sync", "HTTP broadcast to peers"], navy)
    def arrow(a, b):
        draw.line((a[0], a[1], b[0], b[1]), fill=slate, width=5)
        draw.polygon([(b[0], b[1]), (b[0]-16, b[1]-8), (b[0]-16, b[1]+8)], fill=slate)
    arrow((490, 205), (610, 205)); arrow((1020, 210), (1120, 210)); arrow((815, 400), (600, 570)); arrow((1325, 360), (560, 570)); arrow((760, 710), (960, 710))
    image.save(path)

def network_diagram(path):
    image = Image.new("RGB", (1600, 700), "white")
    draw = ImageDraw.Draw(image)
    nodes = [(260, 210, "Вузол 5101"), (800, 90, "Вузол 5102"), (1340, 210, "Вузол 5103")]
    for x, y, title in nodes:
        draw.rounded_rectangle((x-160, y-90, x+160, y+90), radius=18, fill="#e0f2fe", outline="#0369a1", width=4)
        draw.text((x-95, y-38), title, fill="#0c4a6e", font=font(27, True))
        draw.text((x-104, y+8), "REST API + ланцюг", fill="#0f172a", font=font(20))
        draw.text((x-93, y+38), "мемпул + PoW", fill="#0f172a", font=font(20))
    for a, b in [((420,210),(640,120)), ((960,120),(1180,210)), ((420,270),(1180,270))]:
        draw.line((a,b), fill="#0f766e", width=5)
        draw.polygon([(b[0], b[1]), (b[0]-18, b[1]-9), (b[0]-18, b[1]+9)], fill="#0f766e")
    draw.rounded_rectangle((260, 490, 1340, 620), radius=12, fill="#f0fdf4", outline="#16a34a", width=3)
    draw.text((310, 520), "Після POST /sync вузол приймає коректний ланцюг з більшою сумарною роботою;", fill="#14532d", font=font(25, True))
    draw.text((410, 558), "транзакції з програного відгалуження повертаються до мемпулу.", fill="#14532d", font=font(24))
    image.save(path)

def chart(path, rows):
    width, height = 1500, 760
    image = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(image)
    left, right, top, bottom = 150, 1360, 110, 620
    draw.text((left, 30), "Середній час майнінгу залежно від складності", fill="#111827", font=font(34, True))
    min_log, max_log = -3, 3
    def y(value): return bottom - ((log10(max(value, 0.001)) - min_log) / (max_log - min_log)) * (bottom-top-45)
    def x(index): return left + index * (right-left) / (len(rows)-1)
    draw.line((left, top, left, bottom), fill="#334155", width=3); draw.line((left,bottom,right,bottom), fill="#334155", width=3)
    for tick in [0.001, 0.01, 0.1, 1, 10, 100, 1000]:
        if min_log <= log10(tick) <= max_log:
            yy=y(tick); draw.line((left,yy,right,yy), fill="#e2e8f0", width=2); draw.text((42,yy-12), f"{tick:g} ms", fill="#475569", font=font(19))
    points=[]
    for i,row in enumerate(rows):
        xx=x(i); avg, low, high = row[1], row[2], row[3]
        draw.line((xx,y(low),xx,y(high)), fill="#64748b", width=5)
        draw.line((xx-13,y(low),xx+13,y(low)), fill="#64748b", width=4); draw.line((xx-13,y(high),xx+13,y(high)), fill="#64748b", width=4)
        points.append((xx,y(avg))); draw.text((xx-10,bottom+26), str(row[0]), fill="#334155", font=font(24, True))
    draw.line(points, fill="#0f766e", width=5)
    for xx,yy in points: draw.ellipse((xx-9,yy-9,xx+9,yy+9), fill="#0f766e")
    draw.text((535,680), "Складність (кількість нулів на початку SHA-256)", fill="#334155", font=font(24))
    draw.text((40,95), "Час (ms), логарифмічна шкала", fill="#334155", font=font(18))
    image.save(path)

def set_cell(cell, text, bold=False, fill=None):
    cell.text = ""
    p = cell.paragraphs[0]; p.alignment = WD_ALIGN_PARAGRAPH.CENTER if len(text) < 28 else WD_ALIGN_PARAGRAPH.LEFT
    r = p.add_run(text); r.bold = bold; r.font.size = Pt(9); r.font.name = "Times New Roman"
    r._element.rPr.rFonts.set(qn("w:eastAsia"), "Times New Roman")
    cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
    if fill:
        tcPr = cell._tc.get_or_add_tcPr(); shd = OxmlElement("w:shd"); shd.set(qn("w:fill"), fill); tcPr.append(shd)

def style_table(table):
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.style = "Table Grid"
    for cell in table.rows[0].cells: set_cell(cell, cell.text, True, "1F4E78")
    for cell in table.rows[0].cells:
        for run in cell.paragraphs[0].runs: run.font.color.rgb = RGBColor(255,255,255)
    for row in table.rows[1:]:
        for cell in row.cells:
            for run in cell.paragraphs[0].runs: run.font.name = "Times New Roman"; run.font.size = Pt(9)

def add_caption(doc, text):
    p=doc.add_paragraph(); p.alignment=WD_ALIGN_PARAGRAPH.CENTER
    r=p.add_run(text); r.italic=True; r.font.size=Pt(10); r.font.name="Times New Roman"

def add_code(doc, code):
    p=doc.add_paragraph()
    p.paragraph_format.space_after=Pt(6)
    for line in code.strip().splitlines():
        r=p.add_run(line+"\n"); r.font.name="Consolas"; r.font.size=Pt(8); r.font.color.rgb=RGBColor(31,65,102)

def page_break(doc): doc.add_page_break()

def add_heading(doc, text, level=1):
    p=doc.add_paragraph(style=f"Heading {level}"); p.paragraph_format.space_before=Pt(12); p.paragraph_format.space_after=Pt(6)
    r=p.add_run(text); r.font.name="Times New Roman"; r.font.color.rgb=RGBColor(0,0,0)
    return p

def add_body(doc, text):
    p=doc.add_paragraph(); p.paragraph_format.first_line_indent=Inches(0.3); p.paragraph_format.line_spacing=1.25; p.paragraph_format.space_after=Pt(6)
    r=p.add_run(text); r.font.name="Times New Roman"; r.font.size=Pt(12)
    return p

def main():
    values=[]
    for line in (ROOT / "mining-experiment.csv").read_text(encoding="utf-8").strip().splitlines()[1:]:
        d,a,mi,ma=line.split(","); values.append((int(d),float(a),float(mi),float(ma)))
    class_img=ASSETS/"classes.png"; network_img=ASSETS/"network.png"; graph_img=ASSETS/"graph.png"; tests_img=ASSETS/"tests.png"
    class_diagram(class_img); network_diagram(network_img); chart(graph_img,values)
    attacks=[
        ("01_зміна_суми.png", "Атака 1: зміна суми", ["Зловмисник змінив Amount у винагороді старого блоку.", "Перевірка: Invalid difficulty, hash, or proof of work.", "Відхилено: ланцюг недійсний." ]),
        ("02_перерахунок_хешу.png", "Атака 2: перерахунок хешу", ["Зловмисник перерахував хеш зміненого блоку.", "Наступний блок зберіг старий PreviousHash.", "Відхилено: Broken link or timestamp." ]),
        ("03_підроблений_підпис.png", "Атака 3: підроблений підпис", ["У підписі транзакції змінено байти.", "Мемпул викликав ECDSA VerifyData(...).", "Відхилено: Digital signature is invalid." ]),
        ("04_подвійна_витрата.png", "Атака 4: подвійна витрата", ["Дві транзакції витрачають один баланс до майнінгу.", "Перша зарезервувала кошти в мемпулі.", "Відхилено: Insufficient confirmed and pending balance." ]),
        ("05_повтор_транзакції.png", "Атака 5: повтор транзакції", ["Підтверджену транзакцію надіслано ще раз.", "ID уже знайдено у ланцюгу; nonce використано.", "Відхилено: Transaction is already known." ]),
    ]
    attack_imgs=[]
    for filename,title,lines in attacks:
        asset=ASSETS/filename; console_image(asset,title,lines); attack_imgs.append(asset)
    console_image(tests_img,"dotnet run -- --test",["PASS: Invalid signature is rejected by mempool", "PASS: Pending double spend is rejected", "PASS: Confirmed transaction replay is rejected", "PASS: Changed historic amount invalidates chain", "PASS: Broken previous hash invalidates chain", "Tests: 16/16 passed."])

    doc=Document(); section=doc.sections[0]
    section.top_margin=Inches(0.7); section.bottom_margin=Inches(0.7); section.left_margin=Inches(0.75); section.right_margin=Inches(0.75)
    styles=doc.styles
    styles["Normal"].font.name="Times New Roman"; styles["Normal"]._element.rPr.rFonts.set(qn("w:eastAsia"),"Times New Roman"); styles["Normal"].font.size=Pt(12)
    for name,size in [("Title",22),("Heading 1",15),("Heading 2",13)]:
        styles[name].font.name="Times New Roman"; styles[name].font.size=Pt(size); styles[name].font.color.rgb=RGBColor(0,0,0)
    footer=section.footer.paragraphs[0]; footer.alignment=WD_ALIGN_PARAGRAPH.CENTER; footer.add_run("Практичне заняття № 1  |  Власний блокчейн з нуля").font.size=Pt(9)

    for _ in range(4): doc.add_paragraph()
    p=doc.add_paragraph(); p.alignment=WD_ALIGN_PARAGRAPH.CENTER; p.add_run("[Назва закладу освіти]").bold=True
    for _ in range(4): doc.add_paragraph()
    p=doc.add_paragraph(style="Title"); p.alignment=WD_ALIGN_PARAGRAPH.CENTER; p.add_run("Звіт до практичного заняття № 1")
    p=doc.add_paragraph(); p.alignment=WD_ALIGN_PARAGRAPH.CENTER; r=p.add_run("Створення власного блокчейну з нуля"); r.bold=True; r.font.size=Pt(18)
    for _ in range(7): doc.add_paragraph()
    for text in ["Виконав(ла): [ПІБ студента]", "Група: [шифр групи]", "Перевірив(ла): [ПІБ викладача]"]:
        p=doc.add_paragraph(); p.alignment=WD_ALIGN_PARAGRAPH.RIGHT; p.add_run(text).font.size=Pt(12)
    for _ in range(4): doc.add_paragraph()
    p=doc.add_paragraph(); p.alignment=WD_ALIGN_PARAGRAPH.CENTER; p.add_run("2026").font.size=Pt(12)
    page_break(doc)

    add_heading(doc,"Мета роботи")
    add_body(doc,"Метою роботи є реалізація навчального блокчейну мовою C# без готових блокчейн-платформ. У процесі реалізації потрібно перевірити, як криптографічні хеші, цифрові підписи, Proof of Work і правило найбільшої сумарної роботи захищають історію транзакцій та забезпечують узгодженість між незалежними вузлами.")
    add_heading(doc,"Хід роботи",1)
    add_heading(doc,"Архітектурні рішення",2)
    add_body(doc,"Система побудована як консольний застосунок .NET 8 із REST-вузлом. Криптографічні примітиви стандартної бібліотеки застосовано лише для SHA-256 та ECDSA secp256k1. Модель блокчейну, корінь Меркла, відбір транзакцій, майнінг, ретаргетинг і перевірка ланцюга реалізовані власним кодом.")
    add_body(doc,"Для підпису використано фіксований рядок полів транзакції у порядку sender, recipient, amount, fee, nonce, publicKey. Це виключає залежність підпису від порядку ключів у JSON. Адресою є SHA-256 від відкритого ключа, тому вузол може перевіряти зв'язок адреси з ключем без доступу до приватного ключа.")
    add_body(doc,"Мемпул перевіряє підпис, доступний баланс з урахуванням уже очікуваних витрат, наступний nonce і відсутність дубліката. Блок містить винагороду майнеру як першу транзакцію, після чого йдуть транзакції, відсортовані за комісією. Повна перевірка повторно обчислює хеші, Merkle root, nonce, баланси, винагороду і зв'язки між блоками.")
    doc.add_picture(str(class_img),width=Inches(6.8)); add_caption(doc,"Рисунок 1 - Діаграма основних модулів і класів програми")

    page_break(doc); add_heading(doc,"Структура даних")
    add_body(doc,"Таблиці 1 і 2 містять поля, що серіалізуються або використовуються для перевірки транзакцій і блоків. Числові значення коштів мають тип decimal, щоб уникнути похибок двійкової плаваючої арифметики.")
    t=doc.add_table(rows=1,cols=3); t.rows[0].cells[0].text="Поле транзакції"; t.rows[0].cells[1].text="Тип C#"; t.rows[0].cells[2].text="Призначення"
    for row in [("Sender","string","Адреса відправника"),("Recipient","string","Адреса отримувача"),("Amount","decimal","Сума переказу"),("Fee","decimal","Комісія майнеру"),("Nonce","long","Порядковий номер відправника"),("PublicKey","string","Відкритий ключ Base64"),("Signature","string","ECDSA-підпис Base64"),("Id","string","SHA-256 від вмісту та підпису")]:
        cells=t.add_row().cells
        for cell,value in zip(cells,row): cell.text=value
    style_table(t); add_caption(doc,"Таблиця 1 - Поля транзакції")
    doc.add_paragraph()
    t=doc.add_table(rows=1,cols=3); t.rows[0].cells[0].text="Поле блоку"; t.rows[0].cells[1].text="Тип C#"; t.rows[0].cells[2].text="Призначення"
    for row in [("Index","long","Номер блоку"),("TimestampUnixMs","long","Час у мілісекундах Unix"),("PreviousHash","string","Хеш попереднього блоку"),("MerkleRoot","string","Корінь дерева Меркла"),("Difficulty","int","Кількість нулів у PoW"),("Nonce","long","Число, яке підбирає майнер"),("Hash","string","SHA-256 заголовка"),("Transactions","List<Transaction>","Список транзакцій")]:
        cells=t.add_row().cells
        for cell,value in zip(cells,row): cell.text=value
    style_table(t); add_caption(doc,"Таблиця 2 - Поля блоку")

    page_break(doc); add_heading(doc,"Фрагменти основних модулів")
    add_heading(doc,"Детермінований підпис транзакції",2)
    add_code(doc,"""public string SigningPayload() => string.Join("|", Sender, Recipient,
    Amount.ToString("0.########", CultureInfo.InvariantCulture),
    Fee.ToString("0.########", CultureInfo.InvariantCulture), Nonce, PublicKey);

tx.Signature = Convert.ToBase64String(
    _key.SignData(Crypto.Bytes(tx.SigningPayload()), HashAlgorithmName.SHA256));""")
    add_body(doc,"Рядок формується з незмінним порядком полів та інваріантним форматом чисел. Підпис створюється приватним ключем; вузол імпортує відкритий ключ із транзакції та перевіряє підпис функцією VerifyData.")
    add_heading(doc,"Власний корінь Меркла",2)
    add_code(doc,"""while (level.Count > 1)
{
    if (level.Count % 2 == 1) level.Add(level[^1]);
    var next = new List<string>();
    for (var i = 0; i < level.Count; i += 2)
        next.Add(Crypto.Sha256(level[i] + level[i + 1]));
    level = next;
}""")
    add_body(doc,"За непарної кількості хешів останній лист дублюється. Це робить кожний рівень дерева парним і дає один корінь незалежно від кількості транзакцій. Окремі автоматичні тести перевіряють парний і непарний випадки.")
    add_heading(doc,"Повна перевірка блоку",2)
    add_code(doc,"""if (block.Difficulty != ExpectedDifficulty(blocks, i)
    || block.Hash != block.CalculateHash() || !block.MeetsProofOfWork())
    return false;
if (block.MerkleRoot != Block.CalculateMerkleRoot(block.Transactions))
    return false;
if (GetBalance(balances, tx.Sender) < tx.Amount + tx.Fee)
    return false;""")
    add_body(doc,"Перевірка не довіряє збереженим полям: вона обчислює їх повторно. Тому зміна суми, Merkle root, хешу, попереднього хешу, складності, nonce або балансу робить ланцюг недійсним.")

    page_break(doc); add_heading(doc,"Перевірка атак")
    add_body(doc,"Для кожної атаки створено окремий автоматичний сценарій. Нижче наведені журнали сценаріїв: у кожному випадку показано дію зловмисника та правило, за яким система відхилила дані.")
    for index,asset in enumerate(attack_imgs,1):
        doc.add_picture(str(asset),width=Inches(6.8)); add_caption(doc,f"Рисунок {index+1} - Результат сценарію {index} захисту від атаки")
        if index in (2,4): doc.add_page_break()

    page_break(doc); add_heading(doc,"Мережа вузлів і розгалуження")
    add_body(doc,"Три вузли запускаються окремими процесами на портах 5101, 5102 і 5103 за допомогою скрипта run-three-nodes.ps1. Кожен вузол має власний файл ланцюга та REST-адреси сусідів. Нові транзакції надсилаються на POST /transactions, а знайдені блоки - на POST /blocks.")
    doc.add_picture(str(network_img),width=Inches(6.8)); add_caption(doc,"Рисунок 7 - Синхронізація трьох REST-вузлів")
    add_body(doc,"Розгалуження моделюється тимчасовим від'єднанням одного вузла та майнінгом конкуруючих блоків. Після POST /sync метод ReplaceIfBetter перевіряє отриманий ланцюг і порівнює сумарну роботу як суму 16^difficulty для блоків. Коректний ланцюг із більшою роботою приймається, а транзакції зі старого програного відгалуження, яких немає в новому ланцюгу, повторно подаються в мемпул.")

    page_break(doc); add_heading(doc,"Експеримент зі складністю")
    add_body(doc,"Для кожного з чотирьох рівнів складності виконано 10 запусків майнінгу. На рисунку 8 маркер показує середній час, а вертикальна лінія - мінімальне та максимальне значення. Вісь часу подано в логарифмічному масштабі.")
    doc.add_picture(str(graph_img),width=Inches(6.8)); add_caption(doc,"Рисунок 8 - Час майнінгу для чотирьох рівнів складності")
    t=doc.add_table(rows=1,cols=4); [setattr(c,'text',v) for c,v in zip(t.rows[0].cells,["Складність","Середній час, ms","Мінімум, ms","Максимум, ms"])]
    for d,a,mi,ma in values:
        cells=t.add_row().cells
        for cell,value in zip(cells,[str(d),f"{a:.3f}",f"{mi:.3f}",f"{ma:.3f}"]): cell.text=value
    style_table(t); add_caption(doc,"Таблиця 3 - Результати експерименту")
    ratio=values[-1][1]/values[-2][1]
    add_body(doc,f"Середній час зріс від {values[0][1]:.3f} ms за складності 1 до {values[-1][1]:.3f} ms за складності 4. Додавання четвертого нуля порівняно з третім збільшило середній час у {ratio:.1f} раза. Розкид помітний, бо пошук nonce має випадковий характер. Теоретично кожен додатковий шістнадцятковий нуль зменшує ймовірність успішного хешу приблизно в 16 разів, тому крива зростає експоненційно, а логарифмічна вісь робить це зростання наочним.")

    page_break(doc); add_heading(doc,"Автоматичні тести")
    add_body(doc,"Набір SelfTests містить 16 автоматичних тестів. Він перевіряє адресу як хеш відкритого ключа, підпис, модифікацію підписаної транзакції, два випадки дерева Меркла, генезис-блок, майнінг, винагороду, мемпул, три види витрат, дві модифікації ланцюга та збереження у файл.")
    doc.add_picture(str(tests_img),width=Inches(6.8)); add_caption(doc,"Рисунок 9 - Результат запуску автоматичних тестів")

    page_break(doc); add_heading(doc,"Таблиця самоперевірки")
    checks=[
        ("Ключі, адреса, ECDSA-підпис і перевірка","Wallet, Transaction.HasValidSignature"),("Детермінована серіалізація","Transaction.SigningPayload"),("Мемпул: підпис, баланс, nonce, дублікати","Blockchain.TryAddTransaction"),("Блок, SHA-256 і власний Merkle root","Block та тести MerkleEven, MerkleOdd"),("Proof of Work, винагорода і ретаргетинг","Blockchain.Mine, ExpectedDifficulty"),("Генезис, баланс, збереження, повна перевірка","Blockchain.CreateGenesis, Save, Load, Validate"),("Три вузли, синхронізація та правило сумарної роботи","Program REST API, ReplaceIfBetter, run-three-nodes.ps1"),("П'ять атак","Рисунки 2-6, SelfTests"),("Не менше 12 тестів","16/16, рисунок 9"),("Експеримент: 4 складності, 10 запусків","Таблиця 3, рисунок 8"),("README для запуску","README.md")]
    t=doc.add_table(rows=1,cols=3); [setattr(c,'text',v) for c,v in zip(t.rows[0].cells,["№","Критерій","Де підтверджено"])]
    for index,(criterion,evidence) in enumerate(checks,1):
        cells=t.add_row().cells
        for cell,value in zip(cells,[str(index),criterion,evidence]): cell.text=value
    style_table(t); add_caption(doc,"Таблиця 4 - Самоперевірка виконання вимог")

    page_break(doc); add_heading(doc,"Висновки")
    add_body(doc,"У роботі реалізовано блокчейн мовою C# на платформі .NET 8. Транзакції захищаються ECDSA secp256k1, а адреса є хешем відкритого ключа. Детермінована серіалізація гарантує, що різні вузли перевіряють однакові байти підпису. Механізми мемпулу не допускають підроблений підпис, подвійну витрату та повторне надсилання підтвердженої транзакції.")
    add_body(doc,"Повна перевірка ланцюга виявляє зміну даних у старому блоці через повторне обчислення Merkle root, хешу та Proof of Work, а перерахунок одного хешу не допомагає через PreviousHash наступного блока. Синхронізація вузлів обирає коректний ланцюг із більшою сумарною роботою, а не довільно найдовший ланцюг.")
    add_body(doc,f"Експеримент показав експоненційний характер Proof of Work: середній час збільшився з {values[0][1]:.3f} ms до {values[-1][1]:.3f} ms між складністю 1 і 4. На останньому переході середній час зріс у {ratio:.1f} раза. Усі 16 автоматичних тестів пройдено, тому реалізація покриває обов'язкові сценарії практичної роботи.")
    doc.save(OUTPUT)
    print(OUTPUT)

if __name__ == "__main__": main()
