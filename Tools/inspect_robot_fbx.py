"""Inspect the imported FBX model and joint names without launching Unity."""
from pathlib import Path
import struct

data = (Path(__file__).resolve().parents[1] / "Assets/Motion2RGB/ThirdParty/RobotKyle/Model/Robot Kyle.fbx").read_bytes()
version = struct.unpack_from("<I", data, 23)[0]
assert version < 7500

def prop(offset):
    tag = chr(data[offset])
    offset += 1
    formats = {"Y": "h", "C": "?", "I": "i", "F": "f", "D": "d", "L": "q"}
    if tag in formats:
        fmt = "<" + formats[tag]
        return struct.unpack_from(fmt, data, offset)[0], offset + struct.calcsize(fmt)
    if tag in ("S", "R"):
        size = struct.unpack_from("<I", data, offset)[0]
        value = data[offset + 4:offset + 4 + size]
        return value.decode(errors="replace") if tag == "S" else "binary", offset + 4 + size
    count, encoding, size = struct.unpack_from("<III", data, offset)
    return (tag, count, encoding), offset + 12 + size

def nodes(offset, end, indent=0):
    while offset + 13 <= end:
        finish, count, property_size, name_size = struct.unpack_from("<IIIB", data, offset)
        if finish == 0:
            break
        name = data[offset + 13:offset + 13 + name_size].decode()
        cursor = offset + 13 + name_size
        properties = []
        for _ in range(count):
            value, cursor = prop(cursor)
            properties.append(value)
        if name == "Model":
            print(properties)
        nodes(cursor, finish, indent + 1)
        offset = finish

nodes(27, len(data))
