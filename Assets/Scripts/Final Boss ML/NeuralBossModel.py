import json
import platform
import numpy as np
import pandas as pd
from pathlib import Path
from sklearn.preprocessing import StandardScaler
from sklearn.model_selection import train_test_split
from sklearn.mixture import GaussianMixture
from sklearn.metrics import classification_report, confusion_matrix
from tensorflow.keras.models import Sequential
from tensorflow.keras.layers import Dense

# ===== LOAD DATA =====
# TODO potentially move to preprocessing file as used for both models
def get_data_path():
    system = platform.system()

    if system == "Darwin":
        return Path.home() / "Library" / "Application Support" / "DefaultCompany" / "ChronoQuest1"
    elif system == "Windows":
        return Path.home() / "AppData" / "LocalLow" / "DefaultCompany" / "ChronoQuest1"

data_folder = get_data_path()
data_path = data_folder / "session_data.jsonl"

df = pd.read_json(data_path, lines=True)

# ===== FEATURE ENGINEERING =====
# filtering useless sessions
df = df[df["session_duration_seconds"] > 30]  

df["dash_rate"] = df["dash_count"] / df["session_duration_seconds"]
df["jump_rate"] = (df["jump_count"] + df["wall_jump_count"] + df["double_jump_count"]) / df["session_duration_seconds"]
df["melee_rate"] = df["melee_attacks"] / df["session_duration_seconds"]
df["spell_rate"] = df["spell_casts"] / df["session_duration_seconds"]
df["rewind_rate"] = df["rewind_activation_count"] / df["session_duration_seconds"]
df["damage_rate"] = df["damage_taken_total"] / df["session_duration_seconds"]

df["melee_accuracy"] = df["melee_hits"] / (df["melee_attacks"] + 1e-5)
df["spell_accuracy"] = df["spell_hits"] / (df["spell_casts"] + 1e-5)

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

X = df[features]

# ===== SCALE FEATURES =====
scaler = StandardScaler()
X_scaled = scaler.fit_transform(X)

# ===== CREATING LABELS =====
gmm = GaussianMixture(n_components=5, covariance_type="diag", random_state=42)
gmm.fit(X_scaled)

y = gmm.predict(X_scaled) 

print("Cluster distribution:")
print(pd.Series(y).value_counts())

# ===== TRAIN / TEST SPLIT =====
X_train, X_test, y_train, y_test = train_test_split(
    X_scaled, y, test_size=0.2, random_state=42
)

# ===== NEURAL NETWORK MODEL =====
# TODO hyperparameter tuning?? 
model = Sequential([
    Dense(32, activation='relu', input_shape=(X_train.shape[1],)),
    Dense(16, activation='relu'),
    Dense(len(np.unique(y)), activation='softmax')
])

model.compile(
    optimizer='adam',
    loss='sparse_categorical_crossentropy',
    metrics=['accuracy']
)

# ===== TRAIN =====
history = model.fit(
    X_train, y_train,
    epochs=30,
    batch_size=16,
    validation_split=0.2,
    verbose=1
)

# ===== EVALUATION AND METRICS =====
loss, acc = model.evaluate(X_test, y_test)
print("\nTest Accuracy:", acc)

y_pred = np.argmax(model.predict(X_test), axis=1)

print("\nClassification Report:")
print(classification_report(y_test, y_pred))

print("\nConfusion Matrix:")
print(confusion_matrix(y_test, y_pred))
