import socket
import struct

class UnityEnvClient:
    # This script was generated with assistance from OpenAI's GPT-5 model.
    # For details on the classes here that were new to me at the time of writing, see:
    #  
    
    def __init__(self, host='127.0.0.1', port=5005, timeout=5.0):
        self.sock = socket.create_connection((host, port), timeout=timeout)
        self.sock.settimeout(None)
        self.buf = self.sock.makefile('rwb')

    def reset(self, seed=0):
        # op=1 reset, then send seed (int32)
        self.buf.write(struct.pack('<i', 1))
        self.buf.write(struct.pack('<i', seed))
        self.buf.flush()
        # server responds with respLen int32; for reset we send respLen=0
        resp_len_bytes = self.buf.read(4)
        resp_len, = struct.unpack('<i', resp_len_bytes)
        return None

    def step(self, action):
        # action: iterable of floats
        action = list(action)
        self.buf.write(struct.pack('<i', 0))  # op=0 step
        self.buf.write(struct.pack('<i', len(action)))
        for f in action:
            self.buf.write(struct.pack('<f', float(f)))
        self.buf.flush()

        # Read server response size
        resp_len_bytes = self.buf.read(4)
        resp_len, = struct.unpack('<i', resp_len_bytes)
        if resp_len == 0:
            return None
        resp = self.buf.read(resp_len)
        # Unpack: int32 obs_len, obs_len floats, reward float, done byte
        off = 0
        obs_len, = struct.unpack_from('<i', resp, off); off += 4
        obs = []
        for i in range(obs_len):
            v, = struct.unpack_from('<f', resp, off); off += 4
            obs.append(v)
        reward, = struct.unpack_from('<f', resp, off); off += 4
        done_byte, = struct.unpack_from('<B', resp, off); off += 1
        done = bool(done_byte)
        return obs, reward, done

    def close(self):
        try:
            self.sock.close()
        except:
            pass

if __name__ == '__main__':
    client = UnityEnvClient()
    client.reset(seed=123)
    # send a zero action vector of size 6 (Fx,Fy,Fz,Tx,Ty,Tz)
    for i in range(10):
        obs, reward, done = client.step([0.0]*6)
        print(f"step {i}: reward= {reward:.4f}, done= {done}, obs_len= {len(obs) if obs else 0}")
        if done: break
    client.close()
