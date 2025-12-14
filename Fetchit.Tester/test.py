import socket
import time
import random
from typing import List

def create_sip_register_message(
    source_ip: str, 
    source_port: int, 
    destination_ip: str, 
    call_id: str
) -> str:
    """Create a SIP REGISTER message"""
    return f"""REGISTER sip:{destination_ip} SIP/2.0
Via: SIP/2.0/UDP {source_ip}:{source_port};branch=z9hG4bK{random.randint(1000, 9999)}
Max-Forwards: 70
To: <sip:1234@{destination_ip}>
From: <sip:1234@{destination_ip}>;tag={random.randint(10000, 99999)}
Call-ID: {call_id}
CSeq: 1 REGISTER
Contact: <sip:1234@{source_ip}:{source_port}>
Expires: 3600
User-Agent: SIPTester/1.0
Content-Length: 0

"""

def create_sip_invite_message(
    source_ip: str,
    source_port: int,
    destination_ip: str,
    call_id: str,
    caller_number: str = "5551234567",
    callee_number: str = "5557654321"
) -> str:
    """Create a SIP INVITE message with caller ID information"""
    return f"""INVITE sip:{callee_number}@{destination_ip} SIP/2.0
Via: SIP/2.0/UDP {source_ip}:{source_port};branch=z9hG4bK{random.randint(1000, 9999)}
Max-Forwards: 70
To: <sip:{callee_number}@{destination_ip}>
From: "{caller_number}" <sip:{caller_number}@{source_ip}>;tag={random.randint(10000, 99999)}
Call-ID: {call_id}
CSeq: 1 INVITE
Contact: <sip:{caller_number}@{source_ip}:{source_port}>
Content-Type: application/sdp
User-Agent: SIPTester/1.0
Content-Length: 158

v=0
o=- {int(time.time())} 1 IN IP4 {source_ip}
s=SIP Call
c=IN IP4 {source_ip}
t=0 0
m=audio {source_port + 2} RTP/AVP 0 8 101
a=rtpmap:0 PCMU/8000
a=rtpmap:8 PCMA/8000
"""

def create_sip_bye_message(
    source_ip: str,
    source_port: int,
    destination_ip: str,
    call_id: str,
    caller_number: str = "5551234567",
    callee_number: str = "5557654321"
) -> str:
    """Create a SIP BYE message"""
    return f"""BYE sip:{callee_number}@{destination_ip} SIP/2.0
Via: SIP/2.0/UDP {source_ip}:{source_port};branch=z9hG4bK{random.randint(1000, 9999)}
Max-Forwards: 70
To: <sip:{callee_number}@{destination_ip}>;tag={random.randint(10000, 99999)}
From: "{caller_number}" <sip:{caller_number}@{source_ip}>;tag={random.randint(10000, 99999)}
Call-ID: {call_id}
CSeq: 3 BYE
Contact: <sip:{caller_number}@{source_ip}:{source_port}>
User-Agent: SIPTester/1.0
Content-Length: 0

"""

def create_sip_200ok_message(
    source_ip: str,
    source_port: int,
    destination_ip: str,
    call_id: str,
    cseq_method: str = "REGISTER",
    cseq_number: int = 1
) -> str:
    """Create a SIP 200 OK response"""
    return f"""SIP/2.0 200 OK
Via: SIP/2.0/UDP {destination_ip}:{source_port};branch=z9hG4bK{random.randint(1000, 9999)}
To: <sip:1234@{source_ip}>;tag={random.randint(10000, 99999)}
From: <sip:1234@{destination_ip}>;tag={random.randint(10000, 99999)}
Call-ID: {call_id}
CSeq: {cseq_number} {cseq_method}
Contact: <sip:1234@{source_ip}:{source_port}>
User-Agent: SIPTester/1.0
Content-Length: 0

"""

def simulate_sip_traffic() -> None:
    """Simulate SIP traffic by sending various SIP messages"""
    source_ip = "127.0.0.1"  # Simulated source
    source_port = 5070
    destination_ip = "127.0.0.1"  # Simulated destination
    destination_port = 5060

    # Create a UDP socket
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind(('0.0.0.0', source_port))  # Bind to the source port

    try:
        # Generate a few different caller scenarios
        caller_ids: List[str] = [
            "5551234567",
            "5559876543", 
            "4085551212",
            "8005551234",
            "7705551234"
        ]
        
        call_id = f"{random.randint(100000, 999999)}@{source_ip}"
        
        # Send REGISTER message
        print("Sending SIP REGISTER message")
        register_msg = create_sip_register_message(source_ip, source_port, destination_ip, call_id)
        sock.sendto(register_msg.encode(), (destination_ip, destination_port))
        time.sleep(1)
        
        # Send 200 OK response to REGISTER
        print("Sending SIP 200 OK response")
        ok_msg = create_sip_200ok_message(destination_ip, destination_port, source_ip, call_id)
        sock.sendto(ok_msg.encode(), (destination_ip, destination_port))
        time.sleep(1)
        
        # Send a few INVITE messages with different caller IDs
        for caller_id in caller_ids:
            call_id = f"{random.randint(100000, 999999)}@{source_ip}"
            callee_id = random.choice([num for num in caller_ids if num != caller_id])
            
            print(f"Sending SIP INVITE from {caller_id} to {callee_id}")
            invite_msg = create_sip_invite_message(
                source_ip, source_port, destination_ip, call_id, caller_id, callee_id
            )
            sock.sendto(invite_msg.encode(), (destination_ip, destination_port))
            time.sleep(2)
            
            # Send BYE to end the call
            print(f"Sending SIP BYE for call from {caller_id}")
            bye_msg = create_sip_bye_message(
                source_ip, source_port, destination_ip, call_id, caller_id, callee_id
            )
            sock.sendto(bye_msg.encode(), (destination_ip, destination_port))
            time.sleep(1)
    
    finally:
        sock.close()
        print("SIP traffic simulation completed")

if __name__ == "__main__":
    print("Starting SIP traffic simulation...")
    simulate_sip_traffic()