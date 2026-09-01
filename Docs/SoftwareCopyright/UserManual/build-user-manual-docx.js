const fs = require("fs");
const path = require("path");

function loadDocx() {
  try {
    return require("docx");
  } catch (error) {
    const temporaryModule = path.resolve(
      __dirname,
      "../../../Temp/softcopyright-docx-build/node_modules/docx"
    );
    return require(temporaryModule);
  }
}

const {
  AlignmentType,
  BorderStyle,
  Document,
  Footer,
  Header,
  HeadingLevel,
  ImageRun,
  LevelFormat,
  Packer,
  PageBreak,
  PageNumber,
  Paragraph,
  ShadingType,
  Table,
  TableCell,
  TableRow,
  TextRun,
  WidthType,
} = loadDocx();

const sourcePath = path.resolve(
  __dirname,
  "书屿梦行游戏软件V1.0-用户操作说明书-修订稿.md"
);
const outputPath = process.argv[2]
  ? path.resolve(process.argv[2])
  : path.resolve(__dirname, "书屿梦行游戏软件V1.0-用户操作说明书-修订稿.docx");

const bodyFont = "SimSun";
const headingFont = "SimHei";
const pageWidthDxa = 9026;

function inlineRuns(value, options = {}) {
  const cleaned = value
    .replace(/\[([^\]]+)\]\([^)]*\)/g, "$1")
    .replace(/\s{2,}$/g, "");
  const runs = [];
  const tokenPattern = /(\*\*[^*]+\*\*|`[^`]+`)/g;
  let cursor = 0;
  let token;

  while ((token = tokenPattern.exec(cleaned)) !== null) {
    if (token.index > cursor) {
      runs.push(
        new TextRun({
          text: cleaned.slice(cursor, token.index),
          font: bodyFont,
          size: options.size || 21,
          bold: options.bold || false,
          color: options.color,
        })
      );
    }

    const valueToken = token[0];
    if (valueToken.startsWith("**")) {
      runs.push(
        new TextRun({
          text: valueToken.slice(2, -2),
          font: bodyFont,
          size: options.size || 21,
          bold: true,
          color: options.color,
        })
      );
    } else {
      runs.push(
        new TextRun({
          text: valueToken.slice(1, -1),
          font: "Consolas",
          size: Math.max(17, (options.size || 21) - 1),
          color: options.color || "404040",
        })
      );
    }
    cursor = token.index + valueToken.length;
  }

  if (cursor < cleaned.length) {
    runs.push(
      new TextRun({
        text: cleaned.slice(cursor),
        font: bodyFont,
        size: options.size || 21,
        bold: options.bold || false,
        color: options.color,
      })
    );
  }

  if (runs.length === 0) {
    runs.push(new TextRun({ text: "", font: bodyFont, size: options.size || 21 }));
  }
  return runs;
}

function bodyParagraph(value, extra = {}) {
  return new Paragraph({
    children: inlineRuns(value),
    alignment: value.startsWith("图 ") ? AlignmentType.CENTER : AlignmentType.JUSTIFIED,
    spacing: { after: 80, line: 360 },
    ...extra,
  });
}

function headingParagraph(value, level) {
  const headingOptions = {
    1: { size: 30, color: "1F3864", before: 300, after: 150 },
    2: { size: 25, color: "2E5395", before: 220, after: 110 },
    3: { size: 22, color: "404040", before: 160, after: 80 },
  }[level];

  return new Paragraph({
    children: [
      new TextRun({
        text: value,
        bold: true,
        font: headingFont,
        size: headingOptions.size,
        color: headingOptions.color,
      }),
    ],
    heading:
      level === 1
        ? HeadingLevel.HEADING_1
        : level === 2
          ? HeadingLevel.HEADING_2
          : HeadingLevel.HEADING_3,
    spacing: { before: headingOptions.before, after: headingOptions.after },
    keepNext: true,
  });
}

function instructionParagraph(value) {
  return new Paragraph({
    children: inlineRuns(value, { size: 19, color: "7F6000" }),
    shading: { type: ShadingType.CLEAR, fill: "FFF2CC" },
    border: {
      left: { style: BorderStyle.SINGLE, size: 16, color: "D6A000", space: 7 },
      top: { style: BorderStyle.SINGLE, size: 3, color: "E2C15B", space: 4 },
      right: { style: BorderStyle.SINGLE, size: 3, color: "E2C15B", space: 4 },
      bottom: { style: BorderStyle.SINGLE, size: 3, color: "E2C15B", space: 4 },
    },
    indent: { left: 180, right: 80 },
    spacing: { before: 100, after: 140, line: 330 },
  });
}

function bulletParagraph(value) {
  return new Paragraph({
    children: inlineRuns(value),
    numbering: { reference: "manual-bullets", level: 0 },
    spacing: { after: 65, line: 350 },
  });
}

function imageParagraph(relativePath, altText) {
  const imagePath = path.resolve(path.dirname(sourcePath), relativePath);
  if (!fs.existsSync(imagePath)) {
    return instructionParagraph(`申报前插入图片：${altText}；对应路径：${relativePath}`);
  }

  return new Paragraph({
    alignment: AlignmentType.CENTER,
    children: [
      new ImageRun({
        data: fs.readFileSync(imagePath),
        type: "png",
        transformation: { width: 620, height: 349 },
        altText: { title: altText, description: altText, name: altText },
      }),
    ],
    spacing: { before: 100, after: 80 },
  });
}

function markdownTable(lines) {
  const cells = lines
    .filter((line) => !/^\|?\s*:?-{3,}/.test(line.trim()))
    .map((line) =>
      line
        .trim()
        .replace(/^\|/, "")
        .replace(/\|$/, "")
        .split("|")
        .map((cell) => cell.trim())
    );
  const columnCount = Math.max(...cells.map((row) => row.length));
  const columnWidths = new Array(columnCount).fill(Math.floor(pageWidthDxa / columnCount));

  const rows = cells.map(
    (row, rowIndex) =>
      new TableRow({
        tableHeader: rowIndex === 0,
        children: new Array(columnCount).fill("").map((_, columnIndex) =>
          new TableCell({
            width: { size: columnWidths[columnIndex], type: WidthType.DXA },
            shading:
              rowIndex === 0
                ? { type: ShadingType.CLEAR, fill: "D9E2F3" }
                : undefined,
            margins: { top: 55, bottom: 55, left: 65, right: 65 },
            children: [
              new Paragraph({
                children: inlineRuns(row[columnIndex] || "", {
                  size: columnCount >= 5 ? 16 : 18,
                  bold: rowIndex === 0,
                }),
                spacing: { after: 0, line: 270 },
              }),
            ],
          })
        ),
      })
  );

  return new Table({
    width: { size: pageWidthDxa, type: WidthType.DXA },
    columnWidths,
    borders: {
      top: { style: BorderStyle.SINGLE, size: 4, color: "8EAADB" },
      bottom: { style: BorderStyle.SINGLE, size: 4, color: "8EAADB" },
      left: { style: BorderStyle.SINGLE, size: 4, color: "8EAADB" },
      right: { style: BorderStyle.SINGLE, size: 4, color: "8EAADB" },
      insideHorizontal: { style: BorderStyle.SINGLE, size: 2, color: "BFBFBF" },
      insideVertical: { style: BorderStyle.SINGLE, size: 2, color: "BFBFBF" },
    },
    rows,
  });
}

function parseMarkdown() {
  const lines = fs.readFileSync(sourcePath, "utf8").split(/\r?\n/);
  const children = [];
  let index = 0;
  let onCover = true;

  while (index < lines.length) {
    const trimmed = lines[index].trim();

    if (!trimmed) {
      index += 1;
      continue;
    }

    if (trimmed.startsWith("|")) {
      const tableLines = [];
      while (index < lines.length && lines[index].trim().startsWith("|")) {
        tableLines.push(lines[index]);
        index += 1;
      }
      children.push(markdownTable(tableLines));
      children.push(new Paragraph({ children: [], spacing: { after: 90 } }));
      continue;
    }

    const imageMatch = trimmed.match(/^!\[([^\]]*)\]\(([^)]+)\)$/);
    if (imageMatch) {
      children.push(imageParagraph(imageMatch[2], imageMatch[1]));
      index += 1;
      continue;
    }

    if (trimmed.startsWith("# ")) {
      children.push(
        new Paragraph({
          children: [
            new TextRun({
              text: trimmed.slice(2),
              bold: true,
              font: headingFont,
              size: 40,
              color: "1F3864",
            }),
          ],
          alignment: AlignmentType.CENTER,
          spacing: { before: 1300, after: 260 },
        })
      );
      index += 1;
      continue;
    }

    if (trimmed.startsWith("## ")) {
      const heading = trimmed.slice(3);
      if (/^1\./.test(heading) && onCover) {
        children.push(new Paragraph({ children: [new PageBreak()] }));
        onCover = false;
      }

      if (onCover) {
        children.push(
          new Paragraph({
            children: [
              new TextRun({
                text: heading,
                bold: true,
                font: headingFont,
                size: 30,
                color: "2E5395",
              }),
            ],
            alignment: AlignmentType.CENTER,
            spacing: { after: 450 },
          })
        );
      } else {
        children.push(headingParagraph(heading, 1));
      }
      index += 1;
      continue;
    }

    if (trimmed.startsWith("### ")) {
      children.push(headingParagraph(trimmed.slice(4), 2));
      index += 1;
      continue;
    }

    if (trimmed.startsWith("> ")) {
      children.push(instructionParagraph(trimmed.slice(2)));
      index += 1;
      continue;
    }

    if (/^[-*] /.test(trimmed)) {
      children.push(bulletParagraph(trimmed.slice(2)));
      index += 1;
      continue;
    }

    children.push(
      bodyParagraph(trimmed, {
        alignment: onCover ? AlignmentType.CENTER : undefined,
        spacing: onCover ? { after: 140, line: 360 } : { after: 80, line: 360 },
      })
    );
    index += 1;
  }

  return children;
}

const document = new Document({
  creator: "书屿梦行项目组",
  title: "书屿梦行游戏软件 V1.0 用户操作说明书",
  description: "软件著作权用户手册修订工作稿",
  styles: {
    default: {
      document: { run: { font: bodyFont, size: 21 } },
    },
  },
  numbering: {
    config: [
      {
        reference: "manual-bullets",
        levels: [
          {
            level: 0,
            format: LevelFormat.BULLET,
            text: "•",
            alignment: AlignmentType.LEFT,
            style: { paragraph: { indent: { left: 420, hanging: 220 } } },
          },
        ],
      },
    ],
  },
  sections: [
    {
      properties: {
        page: {
          size: { width: 11906, height: 16838 },
          margin: { top: 1300, bottom: 1200, left: 1360, right: 1360, header: 700, footer: 620 },
        },
      },
      headers: {
        default: new Header({
          children: [
            new Paragraph({
              alignment: AlignmentType.CENTER,
              border: {
                bottom: { style: BorderStyle.SINGLE, size: 4, color: "BFBFBF", space: 3 },
              },
              children: [
                new TextRun({
                  text: "书屿梦行游戏软件 V1.0    用户操作说明书",
                  font: bodyFont,
                  size: 18,
                  color: "595959",
                }),
              ],
            }),
          ],
        }),
      },
      footers: {
        default: new Footer({
          children: [
            new Paragraph({
              alignment: AlignmentType.CENTER,
              children: [
                new TextRun({ text: "第 ", font: bodyFont, size: 18, color: "595959" }),
                new TextRun({ children: [PageNumber.CURRENT], font: bodyFont, size: 18, color: "595959" }),
                new TextRun({ text: " 页", font: bodyFont, size: 18, color: "595959" }),
              ],
            }),
          ],
        }),
      },
      children: parseMarkdown(),
    },
  ],
});

Packer.toBuffer(document)
  .then((buffer) => {
    fs.writeFileSync(outputPath, buffer);
    process.stdout.write(`${outputPath}\n${buffer.length} bytes\n`);
  })
  .catch((error) => {
    console.error(error);
    process.exitCode = 1;
  });

