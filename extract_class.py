#!/usr/bin/env python3
"""
Script to extract a specific class definition from a large decompiled C# file.
"""

def extract_class_from_file(filename, class_name, output_file):
    with open(filename, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    # Find the start of the class
    start_line = -1
    for i, line in enumerate(lines):
        if f"public class {class_name}" in line or f"class {class_name}" in line:
            start_line = i
            print(f"Found class {class_name} at line {i+1}")
            break
    
    if start_line == -1:
        print(f"Class {class_name} not found in the file")
        return
    
    # Find the end of the class by counting braces
    brace_count = 0
    end_line = -1
    
    # Find the opening brace of the class
    for i in range(start_line, len(lines)):
        line = lines[i]
        # Count opening and closing braces
        brace_count += line.count('{') - line.count('}')
        
        if '{' in line and brace_count == 1:  # This is the opening brace of the class
            brace_count = 1  # Reset to 1 since we found the opening brace
            continue
            
        if brace_count == 0 and '}' in line:
            end_line = i
            break
    
    if end_line == -1:
        print("Could not find end of class")
        return
    
    # Extract the class definition
    class_lines = lines[start_line:end_line+1]
    
    # Write to output file
    with open(output_file, 'w', encoding='utf-8') as f:
        f.writelines(class_lines)
    
    print(f"Class {class_name} extracted to {output_file}")
    print(f"Lines {start_line+1} to {end_line+1} ({end_line - start_line + 1} lines)")


if __name__ == "__main__":
    import sys
    
    if len(sys.argv) < 2:
        print("Usage: python extract_class.py <class_name> [output_file]")
        print("Example: python extract_class.py InteractionWorker_Insult")
        sys.exit(1)
    
    class_name = sys.argv[1]
    output_file = sys.argv[2] if len(sys.argv) > 2 else f"{class_name}.cs"
    
    extract_class_from_file("decompiled/Assembly-CSharp.decompiled.cs", class_name, output_file)