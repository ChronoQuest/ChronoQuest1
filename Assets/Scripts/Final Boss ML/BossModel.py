import json
import platform
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import seaborn as sns
from pathlib import Path
from sklearn.preprocessing import StandardScaler 
from sklearn.mixture import GaussianMixture
from sklearn.decomposition import PCA
from sklearn.metrics import silhouette_score
from sklearn.model_selection import train_test_split
from sklearn.model_selection import KFold
from scipy.stats import norm
from scipy.spatial.distance import pdist 
from preprocessing import preprocess

# ======= PREPROCESSING =======
X_scaled, scaler, df, feature_names = preprocess(return_df=True)
df_numeric = df[feature_names]

# ======= TRAINING MODEL =======
# tuning for hyperparameter selection using bic and aic
best_gmm = None
best_params = None

lowest_bic = np.inf
lowest_aic = np.inf

bic = []
aic = []

n_components = range(2, 5)
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


# ======== EVALUATION METRICS =======
# TODO move evaluation to notebook
# printing cluster means
cluster_means = df_numeric.groupby("cluster").mean()
print("Cluster Means: ", cluster_means)

for i, mean in cluster_means.iterrows():
    print(f"\nCluster {i}:")
    for feature, value in zip(feature_names, mean):
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

# pca for visualisation
pca = PCA(n_components=2)
X_pca = pca.fit_transform(X_scaled)

plt.scatter(X_pca[:, 0], X_pca[:, 1], c=labels)
plt.title("PCA of Player Behaviour Clusters")
plt.xlabel("PC1")
plt.ylabel("PC2")
plt.show()

print("\n----- Cluster Distance and Separation -----")
cluster_centres = []
for i in np.unique(labels):
    cluster_points = X_pca[labels == i]
    centre = cluster_points.mean(axis=0)
    cluster_centres.append(centre)

cluster_centres = np.array(cluster_centres)

inter_cluster_distances = pdist(cluster_centres)
print("Inter-cluster distances: ", inter_cluster_distances)
print("Mean inter-cluster distance: ", np.mean(inter_cluster_distances))

intra_distances = []
for i in np.unique(labels):
    cluster_points = X_pca[labels == i]
    center = cluster_points.mean(axis=0)

    distances = np.linalg.norm(cluster_points - center, axis = 1)
    intra_distances.extend(distances)

print("Mean intra-cluster distance: ", np.mean(intra_distances))

# evaluating clusters using silhouette score 
score = silhouette_score(X_scaled, labels)
print("\n ----- Silhouette Score:", score, " -----")

# printing how many samples per cluster 
print("\n")
print(df_numeric["cluster"].value_counts())
    

# ====== PREDICTIONS ======
# trying to see how well clusters generalise to unseen data
X_train, X_test = train_test_split(X_scaled, test_size=0.2, random_state=42)

gmm_eval = GaussianMixture(
    n_components=best_params["n_components"],
    covariance_type=best_params["covariance_type"],
    random_state=42,
    n_init=10
)

gmm_eval.fit(X_train)

train_score = gmm_eval.score(X_train)
test_score = gmm_eval.score(X_test)
print("\n--- Train/Test Evaluation ---")
print("Train: ", train_score)
print("Test: ", test_score)


# ====== CROSS VALIDATION ======
kf = KFold(n_splits=5, shuffle=True, random_state=42)
cv_scores = []

for train_idx, test_idx in kf.split(X_scaled):
    X_train, X_test = X_scaled[train_idx], X_scaled[test_idx]

    gmm_cv = GaussianMixture(
        n_components=best_params["n_components"],
        covariance_type=best_params["covariance_type"],
        random_state=42,
        n_init=10
    )

    gmm_cv.fit(X_train)
    cv_scores.append(gmm_cv.score(X_test))

print("\n--- Cross Validation ---")
print("Scores: ", cv_scores)
print("Mean CV Score: ", np.mean(cv_scores))


# ====== FINAL MODEL ======
best_gmm.fit(X_scaled)

# ====== SAVING MODEL =======
# saving GMM details for tactic model
np.save("X_scaled.npy", X_scaled)
np.save("labels.npy", labels)

with open("best_params.json", "w") as f:
    json.dump(best_params, f)

model_data = {
    "n_components": best_gmm.n_components,
    "n_features": best_gmm.means_.shape[1],
    "means": best_gmm.means_.flatten().tolist(),
    "covariances": best_gmm.covariances_.tolist(),
    "weights": best_gmm.weights_.tolist(),
    "scaler_mean": scaler.mean_.tolist(),
    "scaler_scale": scaler.scale_.tolist()
}

unity_path = Path(__file__).resolve().parents[3] / "Assets" / "StreamingAssets"
unity_path.mkdir(parents=True, exist_ok=True)

model_path = unity_path / "gmm_model.json"

with open(model_path, "w") as f: 
    json.dump(model_data, f)
