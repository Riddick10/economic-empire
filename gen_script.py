#!/usr/bin/env python3
import json, os, re, sys
sys.stdout.reconfigure(encoding="utf-8")
from difflib import get_close_matches

DATA_DIR = os.path.join("d:", os.sep, "Maik", "Code", "economic-empire-main", "Data")
DATA_CS = os.path.join("d:", os.sep, "Maik", "Code", "economic-empire-main", "WorldMap.Data.cs")
PROVINCES_CS = os.path.join("d:", os.sep, "Maik", "Code", "economic-empire-main", "WorldMap.Provinces.cs")