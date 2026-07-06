const fs = require('fs');
const path = require('path');

function generateFormMarkdown(options) {
  const lines = [
    '# 地图生成审批表单',
    '',
    `| 项目 | 值 |`,
    `|------|-----|`,
    `| 请求地图名 | ${options.requestedName} |`,
    `| 最终保存名 | ${options.finalName} |`,
    `| 模式 | ${options.mode} |`,
    `| 基础地图（调整模式） | ${options.baseMap || '-'} |`,
    `| 尺寸 | ${options.width} × ${options.height} |`,
    `| AI 尺寸推断理由 | ${options.sizeReason || '-'} |`,
    `| 种子 | ${options.seed} |`,
    `| 用户描述 | ${options.description || '-'} |`,
    `| AI 解析参数 | style=${options.style}, water=${options.water}, obstacle=${options.obstacle}, decoration=${options.decoration} |`,
    `| AI 解析布局 | ${options.layoutSummary || '-'} |`,
    `| 是否覆盖 | ${options.force ? '是' : '否'} |`,
    `| 输出路径 | ${options.outputPath} |`,
    `| 同步 server | ${options.syncServer ? '是' : '否'} |`,
    '',
    '---',
    '',
    '状态：已执行',
    `执行时间：${new Date().toISOString()}`
  ];
  return lines.join('\n');
}

function writeForm(formPath, options) {
  const dir = path.dirname(formPath);
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(formPath, generateFormMarkdown(options), 'utf8');
}

module.exports = { generateFormMarkdown, writeForm };
