from fastapi import FastAPI
from pydantic import BaseModel 
import json
import os

app = FastAPI()
FILE = "leaderboard.json"

# --- data model --- 
class ScoreEntry(BaseModel):
    name: str
    score: int

# --- helpers --- 
def load_scores():
    with open(FILE) as f:
        return json.load(f)
    
def save(data):
    if not os.path.exists(FILE):
        return []
    with open(FILE, "w") as f: 
        json.dump(data, f)

# --- routes --- 
@app.get("/leaderboard")
def get_leaderboard():
    data = load_scores()
    return data

# TODO: define
@app.post("/score")
def add_scores():
    return{"score": "scores"}
