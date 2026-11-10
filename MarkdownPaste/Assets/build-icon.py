"""Generate the Windows icon from app.svg. Requires ffmpeg with librsvg support."""

import pathlib
import struct
import subprocess

directory = pathlib.Path(__file__).resolve().parent
sizes = (16, 20, 24, 32, 40, 48, 64, 128, 256)
images = []
for size in sizes:
    images.append(subprocess.run(
        ["ffmpeg", "-v", "error", "-i", str(directory / "app.svg"),
         "-vf", f"scale={size}:{size}:flags=lanczos", "-frames:v", "1",
         "-f", "image2pipe", "-vcodec", "png", "-"],
        check=True, stdout=subprocess.PIPE,
    ).stdout)

offset = 6 + 16 * len(sizes)
entries = []
for size, image in zip(sizes, images):
    dimension = size if size < 256 else 0
    entries.append(struct.pack("<BBBBHHII", dimension, dimension, 0, 0, 1, 32, len(image), offset))
    offset += len(image)

(directory / "app.ico").write_bytes(
    struct.pack("<HHH", 0, 1, len(sizes)) + b"".join(entries) + b"".join(images)
)
