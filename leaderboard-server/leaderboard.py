from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel 
import json
import os

app = FastAPI()
FILE = "leaderboard.json"

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"]
)

# --- data model --- 
class ScoreEntry(BaseModel):
    name: str
    score: int

# --- helpers --- 
def load_scores():
    if not os.path.exists(FILE):    
        return []
    with open(FILE, "r") as f:
        return json.load(f)
    
def save_scores(data):
    with open(FILE, "w") as f:
        json.dump(data, f, indent=4)

# --- routes --- 
@app.get("/leaderboard")
def get_leaderboard():
    data = load_scores()
    return sorted(data, key=lambda x: x["score"], reverse=True)

@app.post("/score")
def add_scores(entry: ScoreEntry):
    data = load_scores()

    data.append({
        "name": entry.name,
        "score": entry.score
    })

    save_scores(data)
    return{"message": "Score added"}
