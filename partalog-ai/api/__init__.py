"""API router package.

Routers are imported explicitly by main.py according to the active runtime
profile. Keep this module side-effect free so chat-only images do not import
catalog-processing dependencies such as OpenCV, YOLO, or EasyOCR.
"""

__all__: list[str] = []
