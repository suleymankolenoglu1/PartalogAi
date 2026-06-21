"""API package.

Keep this module side-effect free. Individual routers are imported explicitly by
`main.py` according to the active runtime profile, so lightweight chat tests do
not accidentally import YOLO/OCR/catalog-processing dependencies.
"""

__all__: list[str] = []
