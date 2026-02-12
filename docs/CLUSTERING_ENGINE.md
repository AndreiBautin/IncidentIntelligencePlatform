# Clustering engine

## Overview

Incidents are created from log data in two ways:

1. **TF-IDF clustering**: Groups similar error messages; when a cluster’s size exceeds a threshold, an incident is created for that cluster.
2. **Spike detection**: Detects sudden increases in error rate; creates incidents for significant spikes (P1/P2/P3 by severity).

## TF-IDF and cosine similarity

- Log messages are tokenized and weighted with **TF-IDF** (term frequency–inverse document frequency).
- **Cosine similarity** is used to compare messages; messages above a similarity threshold are grouped into the same cluster.
- Config: `Analysis:ClusterSimilarityThreshold` (e.g. 0.5), `Analysis:ClusterSizeThreshold` (e.g. 10). A cluster must have at least `ClusterSizeThreshold` messages to generate an incident.
- In development, “incident sensitivity” (low/medium/high) can adjust the cluster size threshold and which spikes are turned into incidents.

## How incidents are created

- **From clusters**: For each cluster above the effective size threshold, if no existing incident is linked to that cluster in the time window, a new incident is created with the cluster’s representative message and service.
- **From spikes**: For each detected spike (by severity), an incident is created with error count and time range; severity (P1/P2/P3) is derived from spike magnitude.
- Creation is throttled (e.g. delay between incidents, max per analysis run) to avoid flooding.

## Determinism

- Clustering and spike detection are deterministic for a given input and config; no external AI is required for grouping. AI is used only to generate summary and suggested steps for each incident.
