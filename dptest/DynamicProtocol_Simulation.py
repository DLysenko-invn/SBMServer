
from DynamicProtocol import ISDPFrame,ISDPIO,ISDPCmd,ISDP
import threading
import time

from typing import Callable


import math
import random


class RndSine:
    def __init__(self):
        self.o = random.uniform(0, 9999)
        self.f = random.uniform(0.5, 10.0 )
        self.a = random.uniform(0.1, 1)

    def Get(self,t):
        return math.sin(self.f*(t/1000)+self.o)*self.a

    def GetInt(self,t):
        i = int(self.Get(t/1000)*32767)
        return i

    def GetFloat(self,t):
        return self.Get(t)




class EventsGenerator:

    THREAD_WAIT_SEC = 3

    def __init__(self):
        self.thread = None
        self.startevent = threading.Event()
        self.stopevent = threading.Event()
        self.isstopthread = False
        self.loopproctag = None
        self.loopproc = None
        self.loopdelay = 0

    
    def ThreadProc(self):
        self.startevent.set()
        while (not self.isstopthread):
            time.sleep(self.loopdelay)
            self.loopproc(self.loopproctag)
        self.stopevent.set()


    def Start(self,delay_sec:int,proc:Callable,tag:object=None):
        assert proc!=None
        assert delay_sec!=0
        self.Stop()
        self.loopproctag = tag
        self.loopproc = proc
        self.loopdelay = delay_sec
        self.startevent.clear()
        self.stopevent.clear()
        self.isstopthread = False
        self.thread = threading.Thread(target = self.ThreadProc)
        self.thread.start()
        self.startevent.wait()

    def Stop(self):
        if (self.thread != None):
            self.isstopthread = True
            self.stopevent.wait(self.THREAD_WAIT_SEC)
            self.thread = None







class FakeNewportSerial(ISDPIO):

    GID_RESP = (ISDPCmd.ETYPE_RESP << 6) | ( ISDPCmd.GROUPID & 0x3F)
    GID_ASYNC = (ISDPCmd.ETYPE_ASYNC << 6) | ( ISDPCmd.GROUPID & 0x3F)

    def __init__(self):
        self.portid = None
        self.baudrate = None
        self.timeout_sec = 0
        self.isopened = False
        self.currframe = None
        self.hasdata = threading.Event()
        self.mutex = threading.Lock()
        self.outputbuffer = bytearray() 
        self.eventlabel = EventsGenerator()
        self.eventaccel = EventsGenerator()
        self.eventsanity = EventsGenerator()

        self.sx = RndSine()
        self.sy = RndSine()
        self.sz = RndSine()        


    def Open(self,port:str,baud:int,timeout:int):
        self.portid = port
        self.baudrate = baud
        self.timeout_sec = timeout

        self.hasdata.clear()
        self.outputbuffer = bytearray()
        self.isopened = True



    def IsConnected(self):
        return self.isopened

    def Close(self):
        self.eventlabel.Stop()
        self.eventaccel.Stop()
        self.eventsanity.Stop()
        self.isopened = False



    #TIMER_MKS = 0

    def GetMKS(self):
        return int(time.time() * 1000000)
        #self.TIMER_MKS+=1000
        #return self.TIMER_MKS

    def EventLabel(self,tag:object):
        t = self.GetMKS()
        c = self.CMDLabel(t,random.randint(0,9))
        #print("L >",c.ToString())
        self.Put(c.Frame.ToBytes())

    def EventAccel(self,tag:object):
        t = self.GetMKS()
        x =  self.sx.GetInt(t)
        y =  self.sy.GetInt(t)
        z =  self.sz.GetInt(t)
        c = self.CMDAccel(t,x,y,z)
        #print("A >",c.ToString())
        self.Put(c.Frame.ToBytes())

    def EventSanity(self,tag:object):
        t = self.GetMKS()
        x = self.sx.GetFloat(t)
        y = self.sy.GetFloat(t)
        z = self.sz.GetFloat(t)
        c = self.CMDSanity(t,x,y,z)
        #print("S >",c.ToString())
        self.Put(c.Frame.ToBytes())





    def Put(self,data:bytearray):
        if (data==None) or (len(data)==0):
            return 
        self.mutex.acquire()
        try:
            self.outputbuffer+=data          
        finally:
            self.mutex.release()
        self.hasdata.set()



    def Read(self,size:int)->bytearray:
        result = bytearray()
        self.hasdata.wait(self.timeout_sec)
        self.mutex.acquire()
        try:
            if (len(self.outputbuffer)>=size):
                head = self.outputbuffer[:size]
                tail = self.outputbuffer[size:]
                result = head
                self.outputbuffer = tail
                if (len(self.outputbuffer)==0):
                    self.hasdata.clear()
        finally:
            self.mutex.release()

        return result



    def Write(self,data:bytearray):
        #expects full frame as input 
        assert data!=None
        assert len(data)>0
        f = ISDPFrame()
        while not f.IsFinished:
            n = f.nextreadsize
            head = data[:n]
            tail = data[n:]
            f.Put(head)
            data = tail
        assert len(data)==0
        self.ProcessFrame(f)




    def ProcessFrame(self,frame:ISDPFrame):
        cmd = ISDPCmd(frame)

        eid = cmd.EID
        sid = cmd.Payload[0]
        
        if (sid ==ISDP.SID_TYPE_CUSTOM3):
            if (eid==ISDP.EID_START_SENSOR):
                self.eventlabel.Start(1,self.EventLabel)
            if (eid==ISDP.EID_STOP_SENSOR):
                self.eventlabel.Stop()

        if (sid ==ISDP.SID_RAW_ACCELEROMETER):
            if (eid==ISDP.EID_START_SENSOR):
                self.sx = RndSine()
                self.sy = RndSine()
                self.sz = RndSine()
                self.eventaccel.Start(0.01,self.EventAccel)
            if (eid==ISDP.EID_STOP_SENSOR):
                self.eventaccel.Stop()

        if (sid ==ISDP.SID_TYPE_CUSTOM4):
            if (eid==ISDP.EID_START_SENSOR):
                self.sx = RndSine()
                self.sy = RndSine()
                self.sz = RndSine()  
                self.eventsanity.Start(0.01,self.EventSanity)
            if (eid==ISDP.EID_STOP_SENSOR):
                self.eventsanity.Stop()


        p = bytearray([self.GID_RESP,0xFE])
        f = ISDPFrame.FromPayload(p)
        self.Put(f.ToBytes())


    

    @staticmethod
    def to3bytes(n:int):
        return n.to_bytes( 3, 'little', signed=True)

    @staticmethod
    def toQ16(f:float):
        return int( f * (1 << 16) )

    @staticmethod
    def to4bytes(n:int):
        return n.to_bytes( 4, 'little', signed=True)


    #A 30  00 20 9E0EB301 BBFFFF E9FFFF 840400
    def CMDAccel(self,t:int,x:int,y:int,z:int)->ISDPCmd:
        t = t & 0xFFFFFFFF
        p = bytearray([self.GID_ASYNC,0x30, 0x00,ISDP.SID_RAW_ACCELEROMETER])
        p += t.to_bytes(4, 'little')
        p += FakeNewportSerial.to3bytes(x)
        p += FakeNewportSerial.to3bytes(y)
        p += FakeNewportSerial.to3bytes(z)
        f = ISDPFrame.FromPayload(p)
        c = ISDPCmd(f)
        return c

    
        
    #A 30  00 34 A1D6027A 01 04 000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
    def CMDLabel(self,t:int,labelid:int)->ISDPCmd:
        t = t & 0xFFFFFFFF
        p = bytearray([self.GID_ASYNC,0x30, 0x00,ISDP.SID_TYPE_CUSTOM3])
        p += t.to_bytes(4, 'little')
        number_of_valid_bytes = 1
        p += bytearray([number_of_valid_bytes,labelid])
        p += bytearray(64 - number_of_valid_bytes)
        f = ISDPFrame.FromPayload(p)
        c = ISDPCmd(f)
        return c



    #acc data = -1480 1480 63296.
    #A 30  00 35 316c5c06 0c 38faffff c8050000 40f70000 00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000

    def CMDSanity(self,t:int,x:float,y:float,z:float)->ISDPCmd:
        return self.CMDSanityQ16(t, FakeNewportSerial.toQ16(x),FakeNewportSerial.toQ16(y),FakeNewportSerial.toQ16(z))


    def CMDSanityQ16(self,t:int,x:int,y:int,z:int)->ISDPCmd:
        t = t & 0xFFFFFFFF
        p = bytearray([self.GID_ASYNC,0x30, 0x00,ISDP.SID_TYPE_CUSTOM4])
        p += t.to_bytes(4, 'little')
        number_of_valid_bytes = 12
        p += bytearray([number_of_valid_bytes])
        p += FakeNewportSerial.to4bytes(x)
        p += FakeNewportSerial.to4bytes(y)
        p += FakeNewportSerial.to4bytes(z)
        p += bytearray(64 - number_of_valid_bytes)
        f = ISDPFrame.FromPayload(p)
        c = ISDPCmd(f)
        return c