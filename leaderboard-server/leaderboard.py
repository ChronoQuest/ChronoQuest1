from fastapi import FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from fastapi.responses import HTMLResponse
from fastapi.templating import Jinja2Templates 
from pydantic import BaseModel 
import json
import os

FILE = "leaderboard.json"
BASE_DIR = os.path.dirname(os.path.abspath(__file__))

app = FastAPI()
'''templates = Jinja2Templates(directory=os.path.join(BASE_DIR, "templates"))
app.mount("/static", StaticFiles(directory=os.path.join(BASE_DIR, "static")), name="static")''' 

templates = Jinja2Templates(directory="leaderboard-server/templates")

app.mount(
    "/static",
    StaticFiles(directory="leaderboard-server/static"),
    name="static"
)

leaderboard = []

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

''' # --- helpers --- 
def load_scores():
    if not os.path.exists(FILE):    
        return []
    with open(FILE, "r") as f:
        return json.load(f)
    
def save_scores(data):
    with open(FILE, "w") as f:
        json.dump(data, f, indent=4) '''

# --- routes --- 
''' @app.get("/", response_class=HTMLResponse)
def home(request: Request):
    return templates.TemplateResponse(name="index.html", context={"request": request})

@app.get("/", response_class=HTMLResponse)
def home():
    return "<h1>HELLO</h1>'"'''''

@app.get("/test-template", response_class=HTMLResponse)
def test_template(request: Request):
    return templates.TemplateResponse("index.html", {"request": request})

@app.get("/leaderboard")
def get_leaderboard():
    return leaderboard

@app.post("/score")
def add_scores(score: ScoreEntry):
    leaderboard.append(score)
    leaderboard.sort(key=lambda x: x.score, reverse=True)
    return {"message": "score added"}