import socket
import struct

class UnityEnvClient:
    # I am bad at Python. On a totally unrelated note, I have decided that I do not like this language.
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

#Debugging Tools
def write_floats_to_binary(floats):
    """Write a sequence of floats to a binary file."""
    file_path = "C:/Users/bense/Downloads/obs.bin"
    with open(file_path, "wb") as f:
        for value in floats:
            # 'f' packs each number as a 4 byte float
            f.write(struct.pack("f", value))
#/Debugging Tools
             
if __name__ == '__main__':
    client = UnityEnvClient()
    client.reset(seed=123)
    # send a control vector activating the actuators but not the thrusters
    for i in range(1000):
        obs, reward, done = client.step([1.0] * 16 + [0.0] * 24)
        #write_floats_to_binary(obs)
        print(f"step {i}: reward= {reward:.4f}, done= {done}, obs_len= {len(obs) if obs else 0}")
        if done: break
    client.close()

