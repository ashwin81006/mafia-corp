import socket
import json
import time
import urllib.request
import urllib.parse
import urllib.error
import threading
import sys
import random

API_URL = "http://localhost:5130"
GAME_SERVER_PORT = 54555

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
        self.socket = None
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
        
        self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.socket.connect(('127.0.0.1', GAME_SERVER_PORT))
        
        join_msg = {"type": "JOIN_ROOM", "roomCode": self.room_code, "username": self.username, "userId": str(self.user_id)}
        self.socket.send((json.dumps(join_msg) + "\n").encode())
        
        threading.Thread(target=self.listen_loop, daemon=True).start()

    def send(self, msg):
        try:
            self.socket.send((json.dumps(msg) + "\n").encode())
        except:
            pass

    def listen_loop(self):
        while True:
            try:
                data = self.socket.recv(4096).decode('utf-8')
                if not data: break
                for line in data.strip().split('\n'):
                    if line:
                        msg = json.loads(line)
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
        print("Usage: python simulate_bots.py <ROOM_CODE> [NUM_BOTS]")
        print("Example: python simulate_bots.py ABCD 4")
        sys.exit(1)

    room_code = sys.argv[1].upper()
    num_bots = int(sys.argv[2]) if len(sys.argv) > 2 else 4
    
    print(f"Deploying {num_bots} AI Bots to Room {room_code}...")
    
    bots = []
    for i in range(1, num_bots + 1):
        username = f"Bot_00{i}"
        b = Bot(username, "Password123!", room_code)
        b.start()
        bots.append(b)
        time.sleep(0.4)

    print(f"Successfully injected {num_bots} bots! (PC + Phone + {num_bots} bots = {num_bots + 2} players).")
    print("You can now click 'Start Game' from the host.")
    print("Press Ctrl+C anytime to disconnect bots.")
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print("Shutting down bots.")
