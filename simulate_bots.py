import json
import time
import urllib.request
import urllib.parse
import urllib.error
import threading
import sys
import random

try:
    from websocket import create_connection  # type: ignore
except (ImportError, AttributeError):
    try:
        from websocket._core import create_connection  # type: ignore
    except (ImportError, AttributeError):
        import site
        for p in site.getsitepackages():
            if p not in sys.path:
                sys.path.append(p)
        try:
            from websocket import create_connection  # type: ignore
        except (ImportError, AttributeError):
            from websocket._core import create_connection  # type: ignore

# Default to the live Render deployment
API_URL = "https://mafia-corp.onrender.com"
WS_URL = "wss://mafia-corp.onrender.com/ws"

def make_request(endpoint, payload=None, token=None):
    url = f"{API_URL}{endpoint}"
    req = urllib.request.Request(url)
    req.add_header('Content-Type', 'application/json')
    if token:
        req.add_header('Authorization', f'Bearer {token}')
    
    data = None
    if payload:
        data = json.dumps(payload).encode('utf-8')
    
    try:
        with urllib.request.urlopen(req, data=data) as response:
            return json.loads(response.read().decode())
    except urllib.error.HTTPError as e:
        if e.code == 400: return {"error": "Duplicate"}
        return None
    except Exception as e:
        return None

class Bot:
    def __init__(self, username, password, room_code):
        self.username = username
        self.password = password
        self.room_code = room_code
        self.ws = None
        self.user_id = None
        self.token = None
        self.is_alive = True
        self.role = None
        self.client_id = None
        self.all_players = []

    def start(self):
        make_request("/api/auth/register", {"username": self.username, "password": self.password})
        login_res = make_request("/api/auth/login", {"username": self.username, "password": self.password})
        if not login_res:
            print(f"[{self.username}] API Login Failed")
            return
            
        self.token = login_res.get("token")
        me_res = make_request("/api/users/me", token=self.token)
        self.user_id = me_res.get("id") if me_res else random.randint(100, 999)
        
        try:
            self.ws = create_connection(WS_URL)
        except Exception as e:
            print(f"[{self.username}] Failed to connect to WebSocket: {e}")
            return
        
        join_msg = {"type": "JOIN_ROOM", "roomCode": self.room_code, "username": self.username, "userId": str(self.user_id)}
        self.ws.send(json.dumps(join_msg))
        
        threading.Thread(target=self.listen_loop, daemon=True).start()

    def send(self, msg):
        try:
            if self.ws:
                self.ws.send(json.dumps(msg))
        except:
            pass

    def listen_loop(self):
        while True:
            try:
                data = self.ws.recv()
                if not data: break
                msg = json.loads(data)
                self.handle_message(msg)
            except Exception:
                break

    def handle_message(self, msg):
        mtype = msg.get("type")
        if mtype == "ROOM_STATE":
            self.all_players = msg.get("players", [])
            for p in self.all_players:
                if p["username"] == self.username:
                    self.client_id = p["id"]
        elif mtype == "PLAYER_JOINED":
            self.all_players.append(msg.get("player"))
        elif mtype == "ROLE_ASSIGNED":
            self.role = msg.get("role")
            print(f"[{self.username}] Joined Room! Role assigned: {self.role}")
        elif mtype == "PHASE_CHANGED":
            phase = msg.get("phase")
            if not self.is_alive: return
            
            if phase == "NIGHT":
                time.sleep(random.uniform(2, 5))
                alive_others = [p["id"] for p in self.all_players if p.get("isAlive", True) and p["id"] != self.client_id]
                if not alive_others: return
                target = random.choice(alive_others)
                
                if self.role == "MAFIA":
                    self.send({"type": "NIGHT_ACTION", "targetPlayerId": target, "message": "KILL"})
                    print(f"[{self.username}] (Mafia) elected to kill Player #{target}")
                elif self.role == "DOCTOR":
                    self.send({"type": "NIGHT_ACTION", "targetPlayerId": target, "message": "PROTECT"})
                    print(f"[{self.username}] (Doctor) protected Player #{target}")
                elif self.role == "DETECTIVE":
                    self.send({"type": "NIGHT_ACTION", "targetPlayerId": target, "message": "INVESTIGATE"})
                    print(f"[{self.username}] (Detective) investigated Player #{target}")
                    
            elif phase == "VOTING":
                time.sleep(random.uniform(4, 10))
                alive_others = [p["id"] for p in self.all_players if p.get("isAlive", True) and p["id"] != self.client_id]
                if alive_others:
                    target = random.choice(alive_others)
                    self.send({"type": "CAST_VOTE", "targetPlayerId": target})
                    print(f"[{self.username}] cast vote for Player #{target}!")
                    
        elif mtype in ("PLAYER_DIED", "PLAYER_ELIMINATED"):
            dead_id = msg.get("id")
            for p in self.all_players:
                if p["id"] == dead_id:
                    p["isAlive"] = False
            if dead_id == self.client_id:
                self.is_alive = False
                print(f"[{self.username}] has been eliminated.")
        elif mtype == "GAME_OVER":
            winner = msg.get("winner")
            print(f"[{self.username}] GAME OVER! Winner: {winner}")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python simulate_bots.py <ROOM_CODE> [NUM_BOTS] [--local | API_URL WS_URL]")
        print("Example: python simulate_bots.py ABCD 5")
        sys.exit(1)

    room_code = sys.argv[1].upper()
    num_bots = int(sys.argv[2]) if len(sys.argv) > 2 and sys.argv[2].isdigit() else 5

    if "--local" in sys.argv:
        API_URL = "http://localhost:5130"
        WS_URL = "ws://127.0.0.1:54555"
    elif len(sys.argv) > 3 and sys.argv[3].startswith("http"):
        API_URL = sys.argv[3]
        if len(sys.argv) > 4:
            WS_URL = sys.argv[4]
    
    print(f"Deploying {num_bots} AI Bots to Room {room_code} via WebSocket ({WS_URL})...")
    
    bots = []
    for i in range(1, num_bots + 1):
        username = f"Bot_00{i}"
        b = Bot(username, "Password123!", room_code)
        b.start()
        bots.append(b)
        time.sleep(0.4)

    print(f"Successfully injected {num_bots} bots! (You + {num_bots} bots = {num_bots + 1} players).")
    print("You can now click 'Start Game' from the host.")
    print("Press Ctrl+C anytime to disconnect bots.")
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print("Shutting down bots.")
