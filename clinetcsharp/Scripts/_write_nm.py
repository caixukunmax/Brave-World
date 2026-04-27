import pathlib, base64
b64 = open(pathlib.Path(__file__).parent / chr(95) + chr(110) + chr(109) + chr(95) + chr(98) + chr(54) + chr(52) + chr(46) + chr(116) + chr(120) + chr(116), 'r').read()
data = base64.b64decode(b64)
target = pathlib.Path(__file__).parent / 'NetworkManager.cs'
target.write_bytes(data)
print('Written', len(data), 'bytes')
