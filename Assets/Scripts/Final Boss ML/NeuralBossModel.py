import json
import platform
import numpy as np
import pandas as pd
import tensorflow as tf
from pathlib import Path
from sklearn.preprocessing import StandardScaler
from sklearn.model_selection import train_test_split
from sklearn.mixture import GaussianMixture
from sklearn.metrics import classification_report, confusion_matrix
from tensorflow.keras.models import Sequential
from tensorflow.keras.layers import Dense

EPOCHS = 30
BATCH_SIZE = 16 
FINAL_EPOCHS = 15 
# TODO maybe add RUN_EXPERIMENTS = False

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

tf.random.set_seed(42)
np.random.seed(42)

# ===== FEATURE ENGINEERING =====
# filtering out shorter player sessions
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
# TODO print the training and testing split 
# --- training split --- 
X_train, X_test, y_train, y_test = train_test_split(
    X_scaled, y, 
    test_size=0.2, 
    random_state=42
)

# --- validation split ---
X_train2, X_val, y_train2, y_val = train_test_split(
    X_train, y_train,
    test_size=0.2,
    random_state=42
)

# ===== TRAIN =====
# --- epochs tuning ---
train_accs = []
val_accs = []
test_accs = []

model = Sequential([
    Dense(32, activation='relu', input_shape=(X_train.shape[1],)),
    Dense(16, activation='relu'),
    Dense(len(np.unique(y)), activation='softmax')
])

# TODO how do we choose the best optimiser and loss function
model.compile(
    optimizer='adam',
    loss='sparse_categorical_crossentropy',
    metrics=['accuracy']
)

for epoch in range (EPOCHS):
    print(f"\nEpoch {epoch+1}/{EPOCHS}")

    history = model.fit(
        X_train2, y_train2,
        epochs=1,
        batch_size=16,
        validation_data=(X_val, y_val),
        shuffle=False, 
        verbose=1
    )

    train_accs.append(history.history['accuracy'][0])
    val_accs.append(history.history['val_accuracy'][0])

    loss, test_acc = model.evaluate(X_test, y_test, verbose=0)
    test_accs.append(test_acc)

    print(f"Test Accuracy: {test_acc: .4f}")

# --- batch size tuning --- 
batch_sizes = [4, 8, 16, 32, 64]
batch_results = {}

for batch_size in batch_sizes:
    print(f"\nBatch Size: {batch_size}")

    # reintialising model
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

    history = model.fit(
        X_train2, y_train2,
        epochs=FINAL_EPOCHS,
        batch_size=batch_size,
        validation_data=(X_val, y_val),
        shuffle=False,
        verbose=1
    )

    loss, test_acc = model.evaluate(X_test, y_test, verbose=0)
    batch_results[batch_size] = float(test_acc)

    print(f"Test Accuracy: {test_acc:.4f}")

# ===== FINAL MODEL ===== 
# TODO

# ===== EVALUATION AND METRICS =====
# metrics output are related to the final model
loss, acc = model.evaluate(X_test, y_test)
print("\nTest Accuracy:", acc)

y_pred = np.argmax(model.predict(X_test), axis=1)

print("\nClassification Report:")
print(classification_report(y_test, y_pred))

print("\nConfusion Matrix:")
print(confusion_matrix(y_test, y_pred))

print("\nGMM vs Neural Network Agreement: ", np.mean(y_test == y_pred))

# ===== SAVING MODEL & EXPERIMENTS =====
epochs_dir = Path("results/epochs_experiment")
epochs_dir.mkdir(parents=True, exist_ok=True)

epoch_results = {
    "train_acc": train_accs,
    "val_acc": val_accs,
    "test_acc": test_accs
}

with open(epochs_dir / "epoch_results.json", "w") as f:
    json.dump(epoch_results, f, indent=4)

batch_dir = Path("results/batch_experiment")
batch_dir.mkdir(parents=True, exist_ok=True)

with open(batch_dir / "batch_results.json", "w") as f:
    json.dump(batch_results, f, indent=4)

# TODO save model for unity
