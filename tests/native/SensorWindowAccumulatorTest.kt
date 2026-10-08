package com.capstonedesign2026.platform
import kotlin.math.*
fun main(){
 var checks=0
 fun verify(ok:Boolean){check(ok);checks++}
 val a=SensorWindowAccumulator(5000,1000,100000)
 a.add("linear",1001,0.0,0.0,0.0);a.add("linear",1100,1.0,0.0,0.0);a.add("linear",1150,2.0,0.0,0.0)
 a.add("linear",1200,0.0,0.0,0.0);a.add("linear",2300,1.0,0.0,0.0)
 a.add("orientation",2400,Math.toRadians(179.0),0.0,0.0);a.add("orientation",2500,Math.toRadians(-179.0),0.0,0.0)
 a.add("gyro",2600,0.0,0.0,0.0)
 verify(a.closeThrough(5999).isEmpty());val row=a.closeThrough(6000).single()
 verify(row.motionEpisodes==2);verify(row.gyroMean==0.0);verify(abs(abs(row.meanAngles[0]!!)-180)<1e-6)
 verify(row.linearCount==5L);verify(row.startEpochMs==100000L && row.endEpochMs==105000L)
 a.add("gyro",5000,1.0,0.0,0.0);verify(a.lateSamples==1L)
 val empty=a.closeThrough(16000);verify(empty.size==2 && empty.all{it.gyroMean==null && it.orientationCount==0L})
 verify(a.closeThrough(16000).isEmpty())
 val long=SensorWindowAccumulator(600000,0,0)
 for(i in 0..3599){long.closeThrough(i*1000L);long.add("acceleration",i*1000L,0.0,0.0,9.81)}
 val final=long.closeThrough(3600000).single();verify(final.index==5L && final.accelerationCount==600L)
 verify(abs(final.accelerationMean!!-9.81)<1e-9)
 // A burst checkpoint and a stop during the same minute must neither lose nor replay samples.
 val checkpoint=SensorWindowAccumulator(60000,0,100000)
 checkpoint.add("acceleration",1000,0.0,0.0,8.0)
 checkpoint.add("acceleration",2000,0.0,0.0,12.0)
 val first=checkpoint.drainAcceleration()!!
 verify(first.accelerationCount==2L && first.accelerationMean==10.0)
 verify(checkpoint.drainAcceleration()==null)
 checkpoint.add("acceleration",5000,0.0,0.0,9.0)
 val stopped=checkpoint.drainAcceleration()!!
 verify(stopped.startEpochMs==first.startEpochMs && stopped.accelerationCount==1L)
 val closed=checkpoint.closeThrough(60000).single()
 verify(closed.accelerationCount==0L && closed.startEpochMs==first.startEpochMs)
 checkpoint.add("acceleration",61000,0.0,0.0,11.0)
 verify(checkpoint.drainAcceleration()!!.startEpochMs==160000L)
 // Empty intervals retain null means, never a false zero measurement.
 verify(checkpoint.closeThrough(180000).all{it.accelerationMean==null})
 println("PASS $checks sensor summary assertions")
}
