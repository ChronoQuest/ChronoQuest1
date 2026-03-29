import json
import platform
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
from pathlib import Path
from sklearn.preprocessing import StandardScaler 
from sklearn.mixture import GaussianMixture
from sklearn.decomposition import PCA

# load collected data from gameplay for training
def get_data_path():
    system = platform.system()

    if system == "Darwin":
        return Path.home() / "Library" / "Application Support" / "DefaultCompany" / "ChronoQuest1"
    elif system == "Windows":
        return Path.home() / "AppData" / "LocalLow" / "DefaultCompany" / "ChronoQuest1"

data_folder = get_data_path()
data_path = data_folder / "session_data.jsonl"

df = pd.read_json(data_path, lines=True)

features = [
"dash_count",
"jump_count",
"wall_jump_count",
"double_jump_count",
"rewind_activation_count",
"rewind_duration_seconds",

"melee_attacks",
"melee_hits",
"spell_casts",
"spell_hits",
"rain_attack_uses",

"damage_taken_total",
"death_count",

"doors_entered",
"trap_hits",
"tutorial_steps_completed",
"pause_count",

"session_duration_seconds"
]

df_numeric = df[features]
print("Feature count:", df_numeric.shape[1])

# preprocessing/scaling data 
scaler = StandardScaler()
X_scaled  = scaler.fit_transform(df_numeric)

# tuning for hyperparameter selection using bic and aic
best_gmm = None
best_params = None

lowest_bic = np.inf
lowest_aic = np.inf

bic = []
aic = []

n_components = range(1, 7)
cv_types = ["spherical", "tied", "diag", "full"]

for cv_type in cv_types:
    for n_component in n_components:
        gmm = GaussianMixture (
            n_components=n_component,
            covariance_type=cv_type,
            random_state=42,
            n_init=10
        )

        gmm.fit(X_scaled)
        bic.append(gmm.bic(X_scaled))
        aic.append(gmm.aic(X_scaled))

        if bic[-1] < lowest_bic:
            lowest_bic = bic[-1]
            lowest_aic = aic[-1]
            best_gmm = gmm
            best_params = {
                "n_components": n_component, 
                "covariance_type": cv_type
            }

        print(f"Testing: k={n_component}, cov={cv_type}, BIC={bic[-1]:.2f}")

# printing best model and parameters
labels = best_gmm.predict(X_scaled)
df_numeric["cluster"] = labels

print("Best model: ", best_gmm)
print("Best parameters: ", best_params)
print("Lowest BIC: ", lowest_bic)
print("Corresponding AIC: ", lowest_aic)

# printing cluster means
cluster_means = df_numeric.groupby("cluster").mean()
print("Cluster Means: ", cluster_means)

for i, mean in cluster_means.iterrows():
    print(f"\nCluster {i}:")
    for feature, value in zip(features, mean):
        print(f"{feature}: {value:.3f}")

# feature importance using cluster means 
feature_importance = {}

for feature in cluster_means.columns:
    values = cluster_means[feature]
    importance = values.max() - values.min()
    feature_importance[feature] = importance

sorted_features = sorted(feature_importance.items(), key=lambda x: x[1], reverse=True)

print("\n ----- Feature Importance (by cluster separation) -----")
for feature, score in sorted_features:
    print(f"{feature}: {score:.3f}")

# variance and spread for features
variances = df_numeric.drop(columns=["cluster"]).var()
variances = variances.sort_values(ascending=False)

print("\n ----- Feature Variance ----- ")
print(variances)
    
# saving GMM details for tactic model
model_data = {
    "n_components": best_gmm.n_components,
    "n_features": best_gmm.means_.shape[1],
    "means": best_gmm.means_.flatten().tolist(),
    "covariances": best_gmm.covariances_.tolist(),
    "weights": best_gmm.weights_.tolist(),
    "scaler_mean": scaler.mean_.tolist(),
    "scaler_scale": scaler.scale_.tolist()
}

# pca for visualisation
pca = PCA(n_components=2)
X_pca = pca.fit_transform(X_scaled)

plt.scatter(X_pca[:, 0], X_pca[:, 1], c=labels)
plt.title("PCA of Player Behaviour Clusters")
plt.xlabel("PC1")
plt.ylabel("PC2")
plt.show()

unity_path = Path(__file__).resolve().parents[3] / "Assets" / "StreamingAssets"
unity_path.mkdir(parents=True, exist_ok=True)

model_path = unity_path / "gmm_model.json"

with open(model_path, "w") as f: 
    json.dump(model_data, f)
