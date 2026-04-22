from fastapi import FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from fastapi.responses import HTMLResponse
from fastapi.templating import Jinja2Templates 
from pydantic import BaseModel 
from dotenv import load_dotenv
import json
import os 
import psycopg2

load_dotenv()

FILE = "leaderboard.json"
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATABASE_URL = os.getenv("DATABASE_URL")

app = FastAPI() 
app.mount(
    "/static",
    StaticFiles(directory="leaderboard-server/static"),
    name="static"
)

templates = Jinja2Templates(directory="leaderboard-server/templates")

conn = psycopg2.connect(DATABASE_URL)
cursor = conn.cursor()

cursor.execute("""
CREATE TABLE IF NOT EXISTS scores (
    id SERIAL PRIMARY KEY,
    player_name TEXT, 
    score INTEGER,
    strategy TEXT,
    timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP
)
"""
)
conn.commit()

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
    strategy: str

# --- leaderboard routes --- 
@app.get("/", response_class=HTMLResponse)
def template(request: Request):
    return templates.TemplateResponse(
        request=request,
        name="index.html", 
        context={"request": request}
    )

@app.get("/leaderboard")
def get_leaderboard():
    cursor.execute("""
        SELECT 
            ROW_NUMBER() OVER (ORDER BY score DESC) AS rank,
            player_name, 
            score, 
            strategy
        FROM scores
        ORDER BY score DESC
        LIMIT 10
    """)

    rows = cursor.fetchall()

    return [
        {"rank": r[0], "name": r[1], "score": r[2], "strategy": r[3]} 
        for r in rows
    ]

@app.post("/score")
def add_scores(entry: ScoreEntry):
    cursor.execute(
        "INSERT INTO scores (player_name, score, strategy) VALUES (%s, %s)",
        (entry.name, entry.score, entry.strategy)
    )

    conn.commit()
    return {"message": "score added"}

# --- testing routes --- 
@app.get("/test-data")
def test_data(): 
    cursor.execute("SELECT * FROM scores ORDER BY timestamp DESC")
    rows = cursor.fetchall()
    return {"total_rows": len(rows), "recent_entries": rows}

@app.delete("/scores/{name}")
def delete_score(name: str):
    cursor.execute("DELETE FROM scores WHERE player_name = %s", (name,))
    conn.commit()
    return {"message": f"Deleted scores for {name}"}

@app.delete("/scores")
def delete_all_scores():
    cursor.execute("DELETE FROM scores")
    conn.commit()
    return{"message": "All scores deleted"}
