import sys
import re

def extract_class(input_file, output_file, class_name):
    try:
        with open(input_file, 'r', encoding='utf-8') as f_in:
            lines = f_in.readlines()
    except FileNotFoundError:
        print(f"Error: Input file not found at {input_file}")
        return

    class_start_line = -1
    # Regex to find the exact class name, allowing for inheritance or generics
    class_regex = re.compile(r'\b(class|interface)\s+' + re.escape(class_name) + r'\b(?:\s*[:<]|$)')

    # Find the class definition
    for i, line in enumerate(lines):
        if class_regex.search(line):
            class_start_line = i
            break

    if class_start_line == -1:
        print(f"Error: Class '{class_name}' not found.")
        return

    print(f"Found class at line: {class_start_line}")

    # Find the namespace by searching backwards
    namespace = ""
    namespace_line = -1
    for i in range(class_start_line, -1, -1):
        if lines[i].strip().startswith('namespace '):
            namespace = lines[i].strip()
            namespace_line = i
            break

    # Find the opening brace of the class
    class_open_brace_line = -1
    for i in range(class_start_line, min(class_start_line + 100, len(lines))):
        if '{' in lines[i]:
            class_open_brace_line = i
            break

    if class_open_brace_line == -1:
        print("Error: Could not find opening brace of class.")
        return

    # Count braces to find the end of the class
    brace_level = 0
    in_class = False
    start_extracting = False
    
    with open(output_file, 'w', encoding='utf-8') as f_out:
        for i in range(namespace_line if namespace_line != -1 else class_start_line, len(lines)):
            line = lines[i]
            
            # Write namespace if we have one
            if not start_extracting and namespace_line != -1 and i == namespace_line:
                f_out.write(line)
                continue
                
            if not start_extracting and i == class_start_line:
                if namespace_line != -1:
                    f_out.write('\t' + line)
                else:
                    f_out.write(line)
                start_extracting = True
                # Count braces in the class declaration line
                brace_level += line.count('{')
                brace_level -= line.count('}')
                continue
                
            if start_extracting:
                if namespace_line != -1:
                    f_out.write('\t' + line)
                else:
                    f_out.write(line)
                
                brace_level += line.count('{')
                brace_level -= line.count('}')
                
                # Check if we've closed all braces (end of class)
                if brace_level <= 0:
                    if namespace_line != -1:
                        f_out.write('}')
                    break

if __name__ == '__main__':
    if len(sys.argv) != 4:
        print("Usage: python extract_class_better.py <input_file> <output_file> <class_name>")
    else:
        extract_class(sys.argv[1], sys.argv[2], sys.argv[3])