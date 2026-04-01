import json
import platform
import numpy as np
import pandas as pd
from pathlib import Path

def get_data_path():
    system = platform.system()

    if system == "Darwin":
        return Path.home() / "Library" / "Application Support" / "DefaultCompany" / "ChronoQuest1"
    elif system == "Windows":
        return Path.home() / "AppData" / "LocalLow" / "DefaultCompany" / "ChronoQuest1"

data_folder = get_data_path()
data_path = data_folder / "session_data.jsonl"

df = pd.read_json(data_path, lines=True)

print(df.columns)

# TODO: write down actions that the player can execute
actions = ["dash", "jump", ""]