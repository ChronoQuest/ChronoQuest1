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
    score INTEGER
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

# --- routes --- 
@app.get("/", response_class=HTMLResponse)
def template(request: Request):
    return templates.TemplateResponse(
        request=request,
        name="index.html", 
        context={"request": request}
    )

'''@app.get("/debug-template")
def debug_template():
    import os
    return {
        "cwd": os.getcwd(),
        "files_root": os.listdir(),
        "files_server": os.listdir("leaderboard-server"),
        "files_templates": os.listdir("leaderboard-server/templates"),
    }'''

@app.get("/leaderboard")
def get_leaderboard():
    cursor.execute("""
        SELECT player_name, score
        FROM scores
        ORDER BY score DESC
        LIMIT 10
    """)

    rows = cursor.fetchall()

    return [{"name": r[0], "score": r[1]} for r in rows]

@app.post("/score")
def add_scores(entry: ScoreEntry):
    cursor.execute(
        "INSERT INTO scores (player_name, score) VALUES (%s, %s)",
        (entry.name, entry.score)
    )

    conn.commit()
    return {"message": "score added"}
