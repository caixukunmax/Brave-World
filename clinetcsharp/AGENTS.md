# Agent Guidance: 我是勇者

## Project Overview
- Godot 4.x project using GDScript
- Project root: `c:\code\我是勇者`

## Self-Improving Memory
This project uses a tiered memory system. **At the start of every session**, read these files in order:

1. `C:\Users\Administrator\.claude\self-improving\memory.md` (global HOT memory)
2. `c:\code\我是勇者\.claude\self-improving\memory.md` (project HOT memory)

If the user corrects you or gives explicit preferences, log them:
- Global preferences → `C:\Users\Administrator\.claude\self-improving\corrections.md`
- Project preferences → `c:\code\我是勇者\.claude\self-improving\corrections.md`

After 3 successful applications of the same lesson, promote it to the corresponding `memory.md`.

## Communication
- 默认使用中文回复用户
