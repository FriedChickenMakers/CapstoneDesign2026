package com.capstonedesign2026.platform

import kotlin.math.*

/** Constant-memory device experiment only. No raw samples survive a callback. */
internal class SensorWindowAccumulator(val windowMs: Long, val startElapsedMs: Long, val startEpochMs: Long) {
    data class Summary(val index: Long, val startEpochMs: Long, val endEpochMs: Long,
        val startElapsedMs: Long, val endElapsedMs: Long, val motionEpisodes: Int,
        val accelerationCount: Long, val accelerationMean: Double?, val accelerationMax: Double?,
        val gyroCount: Long, val gyroMean: Double?, val orientationCount: Long,
        val meanAngles: List<Double?>, val angleConcentration: List<Double?>,
        val linearCount: Long, val linearMean: Double?, val firstEventElapsedMs: Long?,
        val lastEventElapsedMs: Long?, val maxEventGapMs: Long)
    private class Mean { var n=0L; var sum=0.0; var max=0.0
        fun add(v:Double){ n++;sum+=v;max=kotlin.math.max(max,v) };fun value():Double?=if(n==0L)null else sum/n }
    private var index=0L
    private var acceleration=Mean(); private var gyro=Mean();private var linear=Mean()
    private var orientationCount=0L;private var sine=DoubleArray(3);private var cosine=DoubleArray(3)
    private var first:Long?=null;private var last:Long?=null;private var gap=0L
    private var episodes=0;private var moving=false;private var lastEpisode=Long.MIN_VALUE/2
    var lateSamples=0L;private set
    init {require(windowMs>=1000)}
    fun closeThrough(elapsedMs:Long):List<Summary> {
        val result=mutableListOf<Summary>()
        while(elapsedMs>=startElapsedMs+(index+1)*windowMs){
            val begin=startElapsedMs+index*windowMs
            result.add(Summary(index,startEpochMs+index*windowMs,startEpochMs+(index+1)*windowMs,
                begin,begin+windowMs,episodes,acceleration.n,acceleration.value(),if(acceleration.n==0L)null else acceleration.max,
                gyro.n,gyro.value(),orientationCount,(0..2).map{axis->if(orientationCount==0L || hypot(sine[axis],cosine[axis])<1e-9)null else Math.toDegrees(atan2(sine[axis],cosine[axis]))},
                (0..2).map{axis->if(orientationCount==0L)null else hypot(sine[axis],cosine[axis])/orientationCount},
                linear.n,linear.value(),first,last,gap))
            index++;acceleration=Mean();gyro=Mean();linear=Mean();orientationCount=0;sine=DoubleArray(3);cosine=DoubleArray(3)
            first=null;last=null;gap=0;episodes=0
        }
        return result
    }
    fun add(type:String,elapsedMs:Long,x:Double,y:Double,z:Double) {
        if(elapsedMs<startElapsedMs+index*windowMs){lateSamples++;return}
        require(elapsedMs<startElapsedMs+(index+1)*windowMs){"Close elapsed windows before adding"}
        if(!x.isFinite()||!y.isFinite()||!z.isFinite())return
        if(first==null)first=elapsedMs
        last?.let{gap=max(gap,elapsedMs-it)};last=elapsedMs
        val norm=sqrt(x*x+y*y+z*z)
        when(type){
            "acceleration"->acceleration.add(norm)
            "gyro"->gyro.add(norm)
            "linear"->{linear.add(norm)
                if(!moving && norm>=0.8 && elapsedMs-lastEpisode>=1000){episodes++;moving=true;lastEpisode=elapsedMs}
                if(norm<=0.3)moving=false
            }
            "orientation"->{orientationCount++;doubleArrayOf(x,y,z).forEachIndexed{axis,value->sine[axis]+=sin(value);cosine[axis]+=cos(value)}}
        }
    }
}
