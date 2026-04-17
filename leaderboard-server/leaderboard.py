from fastapi import FastAPI
import json

app = FastAPI()
FILE = "leaderboard.json"

def load_scores():
    try: 
        with open(FILE) as f:
            return json.load(f)
    except:
        return []
    
def save(data):
    with open(FILE, "w") as f: 
        json.dump(data, f)

@app.get("/")
async def root():
    return{"message": "Hello World"}

@app.get("/scores")
async def get_scores():
    return{"scores": "scores"}

@app.post("/score_display")
async def display_scores():
    return{"score": "scores"}
