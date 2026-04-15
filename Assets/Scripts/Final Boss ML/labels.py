import numpy as np 
import pandas as pd 
from sklearn.mixture import GaussianMixture
from preprocessing import preprocess 

# === LABELS BASED ON GMM CLUSTERS ===
def gmm_labels(X_scaled):
    gmm = GaussianMixture(
        n_components=5, 
        covariance_type="diag", 
        random_state=42
    )
    
    labels = gmm.fit_predict(X_scaled)
    return labels, gmm

# === LABELS BASED ON MANUAL RULES ===
def manual_labels(df):
    labels = []

    # TODO properly define the manual rules
    for _, row in df.iterrows():
        # -- aggressive --
        if row["damage_rate"] > 0.4 and row["melee_rate"] > 0.3:
            labels.append(0)
        # -- cautious --
        elif row["rewind_rate"] > 0.3:
            labels.append(1)
        # -- evasive -- 
        else: 
            labels.append(2)

    return np.array(labels)
