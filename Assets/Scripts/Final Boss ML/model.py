import platform
import numpy as np
import pandas as pd
from pathlib import Path
from sklearn.preprocessing import StandardScaler 
from sklearn.mixture import GaussianMixture

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
df_numeric = df.select_dtypes(include=[np.number])

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

n_components_range = range(1,7)
cv_types = ["spherical", "tied", "diag", "full"]

for cv_type in cv_types:
    for n_components in n_components_range:
        gmm = GaussianMixture (
            n_components=n_components,
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
                "n_components": n_components, 
                "covariance_type": cv_type
            }

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
