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
from sklearn.model_selection import KFold
from tensorflow.keras.models import Sequential
from tensorflow.keras.layers import Dense
from tensorflow.keras.layers import Dropout 
from tensorflow.keras.callbacks import EarlyStopping
from preprocessing import preprocess
from labels import gmm_labels, manual_labels

EPOCHS = 30
BATCH_SIZE = 16 
FINAL_EPOCHS = 15 
FINAL_BATCH_SIZE = 8

USE_MANUAL = False

# ===== PREPROCESSING =====
X_scaled, scaler, df, feature_names = preprocess(return_df=True)

tf.random.set_seed(42)
np.random.seed(42)

# ===== CREATING LABELS =====
y_gmm, gmm = gmm_labels(X_scaled)
y_manual = manual_labels(df)

print("Unique GMM labels: ", np.unique(y_gmm))
print("Unique manual labels: ", np.unique(y_manual))
print("Manual label count: ")
print(pd.Series(y_manual).value_counts())

if USE_MANUAL:
    y = y_manual
    print("Using MANUAL labels")
else:
    y = y_gmm
    print("Using GMM labels")

# ===== TRAIN / TEST SPLIT =====
# cross validation on collected data 
kf = KFold(n_splits=5, shuffle=True, random_state=42)

fold_accuracies = []

for fold, (train_idx, test_idx) in enumerate(kf.split(X_scaled)):
    print(f"\n ==== Fold {fold+1} ====")
    
    # training split
    X_train, X_test = X_scaled[train_idx], X_scaled[test_idx]
    y_train, y_test = y[train_idx], y[test_idx]

    # validation split
    X_train2, X_val, y_train2, y_val = train_test_split(
        X_train, y_train, test_size=0.2, random_state=42
    )

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

    model.fit(
        X_train2, y_train2,
        epochs=15,
        batch_size=16,
        validation_data=(X_val, y_val),
        shuffle=True, 
        verbose=0
    )

    loss, acc = model.evaluate(X_test, y_test, verbose=0)
    print(f"Fold {fold+1} Accuracy: {acc:.4f}")

    fold_accuracies.append(acc)

mean_acc = np.mean(fold_accuracies)
std_acc = np.std(fold_accuracies)

print("\nCross-Validation Results: ")
print("Accuracies: ", fold_accuracies)
print(f"Mean Accuracy: , {mean_acc:.4f}")
print(f"Std Dev: {std_acc:.4f}")


# ===== TRAIN =====
# --- epochs tuning ---
# tracking accuracy and loss 
train_accs = []
val_accs = []
test_accs = []
train_losses = []
val_losses = []

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
    
    train_losses.append(history.history['loss'][0])
    val_losses.append(history.history['val_loss'][0])

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
callback = EarlyStopping(monitor='val_loss', patience=5, min_delta=0.001, restore_best_weights=True)

model = Sequential([
    Dense(32, activation='relu', input_shape=(X_train.shape[1],)),
    # Dropout(0.2),
    Dense(16, activation='relu'),
    # Dropout(0.2),
    Dense(len(np.unique(y)), activation='softmax')
])

model.compile(
    optimizer='adam',
    loss='sparse_categorical_crossentropy',
    metrics=['accuracy']
)

history = model.fit(
    X_train2, y_train2,
    epochs=50,
    batch_size=FINAL_BATCH_SIZE,
    validation_data=(X_val, y_val),
    shuffle=True,
    callbacks=[callback],
    verbose=1
)

# ===== EVALUATION AND METRICS =====
# metrics output are related to the final model
print("\nTraining stopped at epoch: ", len(history.history['loss']))

stopped_epoch = callback.stopped_epoch
print("\nStopped at epoch:", stopped_epoch + 1)

loss, acc = model.evaluate(X_test, y_test)
print("\nTest Accuracy:", acc)

y_pred = np.argmax(model.predict(X_test), axis=1)

print("\nClassification Report:")
print(classification_report(y_test, y_pred))

print("\nConfusion Matrix:")
print(confusion_matrix(y_test, y_pred))

print("\nGMM vs Neural Network Agreement: ", np.mean(y_test == y_pred))


# ===== SAVING MODEL & EXPERIMENTS =====
# epochs experiment 
epochs_dir = Path("results/epochs_experiment")
epochs_dir.mkdir(parents=True, exist_ok=True)

epoch_results = {
    "train_acc": train_accs,
    "val_acc": val_accs,
    "test_acc": test_accs,
    "train_loss": train_losses,
    "val_loss": val_losses
}

with open(epochs_dir / "epoch_results.json", "w") as f:
    json.dump(epoch_results, f, indent=4)

# batch size experiment 
batch_dir = Path("results/batch_experiment")
batch_dir.mkdir(parents=True, exist_ok=True)

with open(batch_dir / "batch_results.json", "w") as f:
    json.dump(batch_results, f, indent=4)

# cross-validation
folds_dir = Path("results/folds_experiment")
folds_dir.mkdir(parents=True, exist_ok=True)

folds_results = {
    "fold_acc": fold_accuracies,
    "mean_acc": mean_acc,
    "std_acc": std_acc
}

with open(folds_dir / "folds_results.json", "w") as f:
    json.dump(folds_results, f, indent=4)

# saving model to .json file for unity
weights = model.get_weights()

model_data = {
    "W1": weights[0].tolist(),
    "b1": weights[1].tolist(),
    "W2": weights[2].tolist(),
    "b2": weights[3].tolist(),
    "W3": weights[4].tolist(),
    "b3": weights[5].tolist(),

    "scaler_mean": scaler.mean_.tolist(),
    "scaler_scale": scaler.scale_.tolist()
}

unity_path = Path(__file__).resolve().parents[3] / "Assets" / "StreamingAssets"
unity_path.mkdir(parents=True, exist_ok=True)

model_path = unity_path / "neural_network_model.json"

with open(model_path, "w") as f:
    json.dump(model_data, f)
