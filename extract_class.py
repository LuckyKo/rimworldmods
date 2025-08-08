# This script extracts a C# class definition from a large source file.
# It is designed to work with decompiled code where classes can be very large.
#
# Usage:
# python extract_class.py <input_file> <output_file> <class_name>
#
# Arguments:
#   input_file: The path to the large source file containing the class.
#   output_file: The path to write the extracted class to.
#   class_name: The name of the class to extract.

import sys
import os

def extract_class(input_file, output_file, class_name):
    try:
        with open(input_file, 'r', encoding='utf-8') as f_in:
            lines = f_in.readlines()
    except FileNotFoundError:
        print(f"Error: Input file not found at {input_file}")
        return

    with open(output_file, 'w', encoding='utf-8') as f_out:
        in_class = False
        brace_level = 0

        for line in lines:
            stripped_line = line.strip()
            # Find the start of the class definition.
            if not in_class and f'class {class_name}' in stripped_line:
                in_class = True
                f_out.write(line)
                brace_level += line.count('{')
                brace_level -= line.count('}')
                if brace_level == 0 and '{' in line:
                    # Handle single-line class definitions, though unlikely for this use case.
                    in_class = False
                continue

            # Write the lines of the class to the output file.
            if in_class:
                f_out.write(line)
                brace_level += line.count('{')
                brace_level -= line.count('}')
                # Stop when the closing brace is found.
                if brace_level <= 0:
                    break

if __name__ == '__main__':
    if len(sys.argv) != 4:
        print("Usage: python extract_class.py <input_file> <output_file> <class_name>")
    else:
        extract_class(sys.argv[1], sys.argv[2], sys.argv[3])