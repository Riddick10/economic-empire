#!/usr/bin/env python3
"""
Parse C# source files and generate JSON data files for resource deposits and abundance.
"""
import re
import json
import os

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(BASE_DIR, "Data")


def read_file(path):
    with open(path, "r", encoding="utf-8") as f:
        return f.read()


def extract_hashset_strings(text, varname):
    """Extract all string values from a HashSet<string> declaration."""
    # Match from the variable declaration to its closing };
    pattern = rf'HashSet<string>\s+{varname}\s*=\s*new\([^)]*\)\s*\{{(.*?)\}};'
    m = re.search(pattern, text, re.DOTALL)
    if not m:
        raise ValueError(f"Could not find HashSet {varname}")
    body = m.group(1)
    # Extract all quoted strings, ignoring comments
    # Remove single-line comments first
    lines = body.split('\n')
    strings = []
    for line in lines:
        # Remove single-line comment but keep string content
        # Find quoted strings before any comment
        for sm in re.finditer(r'"([^"]*)"', line):
            val = sm.group(1)
            strings.append(val)
            break  # Only first quoted string per line (the value, not comment content)
    return strings


def extract_deposits(text, varname):
    """Extract all deposit tuples from a List<(string, double, double)> declaration."""
    pattern = rf'List<\(string\s+CountryId,\s*double\s+Lon,\s*double\s+Lat\)>\s+{varname}\s*=\s*new\(\)\s*\{{(.*?)\}};'
    m = re.search(pattern, text, re.DOTALL)
    if not m:
        raise ValueError(f"Could not find List {varname}")
    body = m.group(1)
    deposits = []
    for dm in re.finditer(r'\(\s*"([^"]+)"\s*,\s*([-\d.]+)\s*,\s*([-\d.]+)\s*\)', body):
        country_id = dm.group(1)
        lon = float(dm.group(2))
        lat = float(dm.group(3))
        deposits.append({"countryId": country_id, "lon": lon, "lat": lat})
    return deposits


def generate_resource_deposits():
    """Generate resource-deposits.json from WorldMap.Data.cs and WorldMap.Provinces.cs."""
    data_cs = read_file(os.path.join(BASE_DIR, "WorldMap.Data.cs"))
    provinces_cs = read_file(os.path.join(BASE_DIR, "WorldMap.Provinces.cs"))

    result = {}

    # Resources in WorldMap.Data.cs (both provinces and deposits)
    resources_in_data = {
        "uranium": ("UraniumProvinceNames", "UraniumDeposits"),
        "iron": ("IronProvinceNames", "IronDeposits"),
        "coal": ("CoalProvinceNames", "CoalDeposits"),
        "oil": ("OilProvinceNames", "OilDeposits"),
        "naturalGas": ("NaturalGasProvinceNames", "NaturalGasDeposits"),
    }

    for resource_key, (prov_var, dep_var) in resources_in_data.items():
        provinces = extract_hashset_strings(data_cs, prov_var)
        deposits = extract_deposits(data_cs, dep_var)
        result[resource_key] = {"provinces": provinces, "deposits": deposits}

    # Copper: provinces in WorldMap.Provinces.cs, deposits in WorldMap.Data.cs
    copper_provinces = extract_hashset_strings(provinces_cs, "CopperProvinceNames")
    copper_deposits = extract_deposits(data_cs, "CopperDeposits")
    result["copper"] = {"provinces": copper_provinces, "deposits": copper_deposits}

    # Reorder to match requested output format
    ordered = {}
    for key in ["oil", "naturalGas", "coal", "iron", "copper", "uranium"]:
        ordered[key] = result[key]

    output_path = os.path.join(DATA_DIR, "resource-deposits.json")
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(ordered, f, indent=2, ensure_ascii=False)
    print(f"Written: {output_path}")
    return ordered


def generate_resource_abundance():
    """Generate resource-abundance.json from ResourceAbundance.cs."""
    cs_text = read_file(os.path.join(DATA_DIR, "ResourceAbundance.cs"))

    # Find the _data dictionary block
    pattern = r'Dictionary<string,\s*CountryResources>\s+_data\s*=\s*new\(\)\s*\{(.*?)\};'
    m = re.search(pattern, cs_text, re.DOTALL)
    if not m:
        raise ValueError("Could not find _data dictionary")
    body = m.group(1)

    result = {}
    # Match each entry: ["USA"] = new(1.00f, 1.00f, 0.097f, 0.046f, 0.205f, 0.011f),
    entry_pattern = r'\["([^"]+)"\]\s*=\s*new\(\s*([-\d.]+)f\s*,\s*([-\d.]+)f\s*,\s*([-\d.]+)f\s*,\s*([-\d.]+)f\s*,\s*([-\d.]+)f\s*,\s*([-\d.]+)f\s*\)'
    for em in re.finditer(entry_pattern, body):
        country_id = em.group(1)
        oil = float(em.group(2))
        gas = float(em.group(3))
        coal = float(em.group(4))
        iron = float(em.group(5))
        copper = float(em.group(6))
        uranium = float(em.group(7))
        result[country_id] = {
            "oil": oil,
            "naturalGas": gas,
            "coal": coal,
            "iron": iron,
            "copper": copper,
            "uranium": uranium,
        }

    output_path = os.path.join(DATA_DIR, "resource-abundance.json")
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(result, f, indent=2, ensure_ascii=False)
    print(f"Written: {output_path}")
    return result


def verify():
    """Load both JSON files back and print summary counts."""
    print("\n=== Verification ===\n")

    deposits_path = os.path.join(DATA_DIR, "resource-deposits.json")
    with open(deposits_path, "r", encoding="utf-8") as f:
        deposits = json.load(f)
    print(f"resource-deposits.json: valid JSON, {len(deposits)} resources")
    for key, val in deposits.items():
        print(f"  {key}: {len(val['provinces'])} provinces, {len(val['deposits'])} deposits")

    abundance_path = os.path.join(DATA_DIR, "resource-abundance.json")
    with open(abundance_path, "r", encoding="utf-8") as f:
        abundance = json.load(f)
    print(f"\nresource-abundance.json: valid JSON, {len(abundance)} countries")

    # Verify UTF-8 special characters preserved
    ugra_found = False
    for res_data in deposits.values():
        for prov in res_data["provinces"]:
            if "Ugra" in prov:
                print(f"\n  en-dash check: '{prov}'")
                # Check for U+2013 en-dash
                if "\u2013" in prov:
                    print("  -> en-dash (U+2013) correctly preserved!")
                    ugra_found = True
                else:
                    print("  -> WARNING: en-dash NOT found!")
    if not ugra_found:
        print("\n  WARNING: Could not find Ugra province to verify en-dash")


if __name__ == "__main__":
    print("Generating resource-deposits.json...")
    deposits = generate_resource_deposits()
    print("\nGenerating resource-abundance.json...")
    abundance = generate_resource_abundance()
    verify()
