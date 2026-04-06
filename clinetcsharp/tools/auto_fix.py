#!/usr/bin/env python3
"""
Godot 自动化错误检测和修复工具
"""

import json
import re
import subprocess
import sys
from pathlib import Path
from typing import List, Dict, Optional, Tuple
from dataclasses import dataclass
from enum import Enum

# 项目路径
PROJECT_ROOT = Path(__file__).parent.parent
GODOT_DIR = PROJECT_ROOT / ".godot"
LOG_FILE = GODOT_DIR / "auto_run_log.txt"


class ErrorType(Enum):
    PARSE_ERROR = "parse_error"
    SCRIPT_ERROR = "script_error"
    MISSING_FILE = "missing_file"
    NODE_NOT_FOUND = "node_not_found"
    SIGNAL_ERROR = "signal_error"
    AUTOLOAD_ERROR = "autoload_error"
    TYPE_MISMATCH = "type_mismatch"
    UNKNOWN = "unknown"


@dataclass
class GodotError:
    error_type: ErrorType
    file_path: Optional[Path]
    line_number: int
    message: str
    raw_text: str


class ErrorParser:
    """解析 Godot 日志中的错误"""
    
    PATTERNS = {
        ErrorType.PARSE_ERROR: re.compile(
            r'ERROR:.*Parse Error.*at.*res://([^:]+):(\d+).*\n.*?(.*)',
            re.MULTILINE
        ),
        ErrorType.SCRIPT_ERROR: re.compile(
            r'SCRIPT ERROR:.*res://([^:]+):(\d+).*-\s*(.*)',
            re.MULTILINE
        ),
        ErrorType.MISSING_FILE: re.compile(
            r'ERROR:.*Cannot open file.*res://([^\s\']+)',
            re.MULTILINE
        ),
        ErrorType.NODE_NOT_FOUND: re.compile(
            r'ERROR:.*Node not found: "([^"]+)".*\n.*at.*res://([^:]+):(\d+)',
            re.MULTILINE
        ),
        ErrorType.AUTOLOAD_ERROR: re.compile(
            r'ERROR:.*Failed to load Autoload.*res://([^\s\']+)',
            re.MULTILINE
        ),
    }
    
    def parse_log(self, log_content: str) -> List[GodotError]:
        errors = []
        
        for error_type, pattern in self.PATTERNS.items():
            for match in pattern.finditer(log_content):
                if error_type == ErrorType.NODE_NOT_FOUND:
                    node_path = match.group(1)
                    file_path = PROJECT_ROOT / match.group(2)
                    line_no = int(match.group(3))
                    message = f"Node not found: {node_path}"
                elif error_type == ErrorType.MISSING_FILE:
                    file_path = PROJECT_ROOT / match.group(1)
                    line_no = 0
                    message = f"Missing file: {file_path.name}"
                elif error_type == ErrorType.AUTOLOAD_ERROR:
                    file_path = PROJECT_ROOT / match.group(1)
                    line_no = 0
                    message = f"Autoload failed: {file_path.name}"
                else:
                    file_path = PROJECT_ROOT / match.group(1)
                    line_no = int(match.group(2))
                    message = match.group(3).strip()
                
                errors.append(GodotError(
                    error_type=error_type,
                    file_path=file_path,
                    line_number=line_no,
                    message=message,
                    raw_text=match.group(0)
                ))
        
        return errors


class AutoFixer:
    """自动修复 Godot 错误"""
    
    def __init__(self):
        self.fixes_applied = []
        self.fixes_failed = []
    
    def fix(self, error: GodotError) -> bool:
        """尝试修复单个错误"""
        fix_methods = {
            ErrorType.PARSE_ERROR: self._fix_parse_error,
            ErrorType.SCRIPT_ERROR: self._fix_script_error,
            ErrorType.MISSING_FILE: self._fix_missing_file,
            ErrorType.NODE_NOT_FOUND: self._fix_node_not_found,
            ErrorType.AUTOLOAD_ERROR: self._fix_autoload_error,
        }
        
        method = fix_methods.get(error.error_type)
        if method:
            try:
                success = method(error)
                if success:
                    self.fixes_applied.append(error)
                else:
                    self.fixes_failed.append(error)
                return success
            except Exception as e:
                print(f"  [AutoFix] 修复失败: {e}")
                self.fixes_failed.append(error)
                return False
        else:
            print(f"  [AutoFix] 暂无修复策略: {error.error_type.value}")
            self.fixes_failed.append(error)
            return False
    
    def _fix_parse_error(self, error: GodotError) -> bool:
        """修复解析错误（如 .tscn 文件格式问题）"""
        if not error.file_path or not error.file_path.exists():
            return False
        
        content = error.file_path.read_text(encoding='utf-8')
        
        # 修复常见的编码问题
        if '\ufffd' in content:  # 替换字符（乱码）
            print(f"  [AutoFix] 检测到乱码，尝试修复: {error.file_path.name}")
            # 这里可以添加具体的修复逻辑
            return False  # 暂不支持自动修复，需要人工检查
        
        return False
    
    def _fix_script_error(self, error: GodotError) -> bool:
        """修复脚本错误"""
        if not error.file_path or not error.file_path.exists():
            return False
        
        content = error.file_path.read_text(encoding='utf-8')
        lines = content.split('\n')
        
        if error.line_number <= 0 or error.line_number > len(lines):
            return False
        
        line_idx = error.line_number - 1
        line = lines[line_idx]
        
        # 修复未定义变量（常见错误）
        if "Identifier" in error.message and "not declared" in error.message:
            var_name = re.search(r"Identifier '([^']+)'", error.message)
            if var_name:
                var = var_name.group(1)
                # 在文件顶部添加变量声明
                lines.insert(0, f"var {var} = null  # Auto-fixed")
                error.file_path.write_text('\n'.join(lines), encoding='utf-8')
                print(f"  [AutoFix] 添加未定义变量声明: {var}")
                return True
        
        # 修复方法未找到
        if "The method" in error.message and "isn't declared" in error.message:
            method_match = re.search(r"The method '([^']+)'", error.message)
            if method_match:
                method_name = method_match.group(1)
                # 在文件末尾添加空方法
                lines.append(f"\nfunc {method_name}():\n    pass  # Auto-fixed")
                error.file_path.write_text('\n'.join(lines), encoding='utf-8')
                print(f"  [AutoFix] 添加缺失方法: {method_name}")
                return True
        
        return False
    
    def _fix_missing_file(self, error: GodotError) -> bool:
        """修复缺失文件"""
        if not error.file_path:
            return False
        
        # 检查是否是脚本文件
        if error.file_path.suffix == '.gd':
            # 创建空脚本文件
            error.file_path.parent.mkdir(parents=True, exist_ok=True)
            error.file_path.write_text("extends Node\n\n# Auto-created\n", encoding='utf-8')
            print(f"  [AutoFix] 创建缺失脚本: {error.file_path.name}")
            return True
        
        return False
    
    def _fix_node_not_found(self, error: GodotError) -> bool:
        """修复节点未找到"""
        # 这种错误通常需要人工检查场景结构
        print(f"  [AutoFix] 节点未找到，需要人工检查: {error.message}")
        return False
    
    def _fix_autoload_error(self, error: GodotError) -> bool:
        """修复 Autoload 错误"""
        if not error.file_path:
            return False
        
        # 检查 autoload 脚本是否存在语法错误
        if error.file_path.exists():
            content = error.file_path.read_text(encoding='utf-8')
            # 检查是否有明显的语法问题
            if 'extends ' not in content:
                content = "extends Node\n\n" + content
                error.file_path.write_text(content, encoding='utf-8')
                print(f"  [AutoFix] 添加缺失的 extends: {error.file_path.name}")
                return True
        
        return False


def run_godot_check() -> Tuple[bool, List[GodotError]]:
    """运行 Godot 检查并返回结果"""
    print("[AutoFix] 启动 Godot 检查...")
    
    ps_script = Path(__file__).parent / "godot_runner.ps1"
    
    try:
        result = subprocess.run(
            ["powershell", "-ExecutionPolicy", "Bypass", "-File", str(ps_script), "-Mode", "check"],
            capture_output=True,
            text=True,
            timeout=60
        )
        
        # 解析 JSON 输出
        try:
            output = json.loads(result.stdout)
            success = output.get('Success', False)
        except:
            success = result.returncode == 0
        
    except subprocess.TimeoutExpired:
        print("[AutoFix] Godot 检查超时")
        success = False
    except Exception as e:
        print(f"[AutoFix] 运行失败: {e}")
        success = False
    
    # 读取日志并解析错误
    if LOG_FILE.exists():
        log_content = LOG_FILE.read_text(encoding='utf-8', errors='ignore')
        parser = ErrorParser()
        errors = parser.parse_log(log_content)
    else:
        errors = []
    
    return success, errors


def main():
    print("=" * 60)
    print("Godot 自动化错误检测和修复工具")
    print("=" * 60)
    print()
    
    # 运行检查
    success, errors = run_godot_check()
    
    if not errors:
        print("[AutoFix] 未发现错误！")
        return 0
    
    print(f"[AutoFix] 发现 {len(errors)} 个错误:")
    for i, error in enumerate(errors, 1):
        print(f"  {i}. [{error.error_type.value}] {error.message}")
        if error.file_path:
            print(f"     文件: {error.file_path.name}:{error.line_number}")
    print()
    
    # 尝试自动修复
    print("[AutoFix] 开始自动修复...")
    fixer = AutoFixer()
    
    for error in errors:
        print(f"  修复 [{error.error_type.value}]...", end=" ")
        fixed = fixer.fix(error)
        print("成功" if fixed else "失败/跳过")
    
    print()
    print("=" * 60)
    print("修复结果:")
    print(f"  成功: {len(fixer.fixes_applied)}")
    print(f"  失败: {len(fixer.fixes_failed)}")
    
    if fixer.fixes_applied:
        print()
        print("已应用的修复:")
        for error in fixer.fixes_applied:
            print(f"  ✓ {error.error_type.value}: {error.message}")
    
    if fixer.fixes_failed:
        print()
        print("需要人工修复的错误:")
        for error in fixer.fixes_failed:
            print(f"  ✗ {error.error_type.value}: {error.message}")
            if error.file_path:
                print(f"    位置: {error.file_path.name}:{error.line_number}")
    
    print("=" * 60)
    
    return 0 if len(fixer.fixes_applied) == len(errors) else 1


if __name__ == "__main__":
    sys.exit(main())
