import platform
import pandas as pd
import numpy as np
from pathlib import Path
from sklearn.preprocessing import StandardScaler

# ===== PATH HANDLING =====
def get_data_path():
    system = platform.system()

    if system == "Darwin":
        return Path.home() / "Library" / "Application Support" / "DefaultCompany" / "ChronoQuest1"
    elif system == "Windows":
        return Path.home() / "AppData" / "LocalLow" / "DefaultCompany" / "ChronoQuest1"

# ===== LOAD DATA =====
def load_data():
    data_folder = get_data_path()
    data_path = data_folder / "session_data.jsonl"

    df = pd.read_json(data_path, lines=True)
    return df

# ===== FEATURE ENGINEERING =====
def feature_engineering(df):
    df = df[df["session_duration_seconds"] > 30]  

    df["dash_rate"] = df["dash_count"] / df["session_duration_seconds"]
    df["jump_rate"] = (df["jump_count"] + df["wall_jump_count"] + df["double_jump_count"]) / df["session_duration_seconds"]
    df["melee_rate"] = df["melee_attacks"] / df["session_duration_seconds"]
    df["spell_rate"] = df["spell_casts"] / df["session_duration_seconds"]
    df["rewind_rate"] = df["rewind_activation_count"] / df["session_duration_seconds"]
    df["damage_rate"] = df["damage_taken_total"] / df["session_duration_seconds"]

    df["melee_accuracy"] = df["melee_hits"] / (df["melee_attacks"] + 1e-5)
    df["spell_accuracy"] = np.where(
        df["spell_casts"] > 0, 
        df["spell_hits"] / df["spell_casts"],
        0
    )

    return df

# ===== FEATURE SELECTION =====
def get_features(df):
    features = [
        "dash_rate",
        "jump_rate",
        "melee_rate",
        "spell_rate",
        "rewind_rate",
        "damage_rate",
        "melee_accuracy",
        "spell_accuracy"
    ]

    return df[features], features

# ===== SCALE FEATURES =====
def scale_features(X):
    scaler = StandardScaler()
    X_scaled = scaler.fit_transform(X)

    return X_scaled, scaler

# ===== FULL PIPELINE =====
def preprocess(return_df=False):
    df = load_data()
    df = feature_engineering(df)

    X, feature_names = get_features(df)
    X_scaled, scaler = scale_features(X)

    if return_df:
        return X_scaled, scaler, df, feature_names
    
    return X_scaled, scaler, feature_names
