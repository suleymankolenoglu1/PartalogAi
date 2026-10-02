# PartalogAi

> [!NOTE]
> **Archived R&D Prototype**
>
> PartalogAi is the continuation of the original Partalog prototype.
>
> The project explored a technical-catalog-based industrial spare-parts platform, including catalog processing, YOLO-based region detection, and visual product retrieval.
>
> Development was discontinued after technical experiments showed limitations in visual-only retrieval and the product concept raised copyright/licensing concerns around third-party technical catalogs.
>
> This repository is preserved as an engineering case study and development history.

## Overview

PartalogAi started as an attempt to build a SaaS platform where industrial companies could discover and purchase spare parts directly through machinery technical catalogs.

The idea was to transform static technical catalogs into interactive product-discovery experiences and later extend the platform with AI-assisted visual search.

During development, the project evolved into an R&D environment for experimenting with computer vision, catalog processing, visual retrieval, backend services, and industrial product search.

## Original Product Idea

The intended workflow was:

1. Import technical machinery catalogs.
2. Detect product and spare-part regions inside catalog pages.
3. Convert catalog pages into interactive product-discovery interfaces.
4. Allow users to identify and search for spare parts directly from catalog content.
5. Eventually connect identified parts with purchasing workflows.

## Computer Vision Experiments

### YOLO-based Catalog Region Detection

I trained and experimented with YOLO-based object detection to identify relevant regions and hotspots inside technical catalog pages.

The goal was to automatically detect clickable product or spare-part areas instead of defining them manually.

This work provided practical experience with:

- object detection workflows
- dataset preparation
- model training
- inference pipelines
- technical document image processing

### DINOv3 Visual Retrieval

I also experimented with DINOv3-based visual retrieval for industrial spare-part search.

The objective was to retrieve visually similar products from catalog and product-image collections.

In practice, many industrial spare parts are visually very similar while differing in dimensions, compatibility, machine model, or technical function.

As a result, visual similarity alone was not reliable enough for the product-search accuracy I wanted.

This became an important design lesson for my later work on catalog-aware and agentic product-search systems.

## Why the Project Was Discontinued

The project was eventually discontinued for two main reasons.

### Technical Limitations

Visual-only retrieval was not reliable enough for fine-grained industrial spare-part identification.

Industrial products often require additional context such as:

- machine compatibility
- technical specifications
- dimensions
- function
- product codes
- structured catalog information

### Catalog Licensing and Copyright

The original product model depended heavily on third-party machinery technical catalogs.

Using and commercializing these catalogs at scale introduced copyright and licensing concerns that made the original SaaS model difficult to pursue safely.

Rather than continuing to invest in a product with unresolved data-rights constraints, I decided to stop development.

## Key Takeaways

This project taught me several lessons that later influenced how I design AI systems:

- Technical feasibility does not automatically mean product feasibility.
- Data ownership and licensing should be validated early.
- Visual similarity is not equivalent to product identity.
- Industrial search often requires combining structured data, technical context, and AI reasoning.
- Experimental results should guide architecture decisions instead of forcing an initial approach to work.

## Technologies Explored

- Python
- YOLO
- DINOv3
- Computer Vision
- FastAPI
- .NET
- Angular
- PostgreSQL
- Docker
- REST APIs

## Status

**Archived / Discontinued R&D Prototype**

The project is no longer under active development.

Its experiments and lessons influenced my later work on production industrial applications and agentic product-search systems.

---

Earlier development history is available in the archived
[Partalog repository](https://github.com/suleymankolenoglu1/Partalog).
