package com.capstonedesign2026.platform

private class Store : MeasurementStore {
    var value=MeasurementState(); var fail=false
    override fun load()=value
    override fun save(state: MeasurementState) { if(fail)throw java.io.IOException("Synthetic failure");value=state }
}
fun main() {
    val now=1800000000000L; val binding="a".repeat(64);var seq=0;val store=Store()
    fun queue()=MeasurementBuffer(store) { (++seq).toString(16).padStart(32,'0') }
    var q=queue()
    fun event(id:Int, at:Long=now)=MeasurementRecord(id.toString(16).padStart(32,'0'),kind="notification_opened",occurred=at,appVersion="synthetic",dataset="synthetic")
    check(!q.add(event(1),now));q.configure(binding,true,now)
    val collection=q.state.collection
    check(!q.add(event(2,now-1),now));check(q.add(event(1),now));check(q.add(event(1),now));check(q.state.events.size==1)
    check(q.batch(now).size==1)
    try {q.acknowledge(binding,collection,setOf(event(9).eventId),setOf(event(1).eventId));error("Unsent ack accepted")}catch(_:IllegalArgumentException){}
    q.acknowledge("b".repeat(64),collection,setOf(event(1).eventId),setOf(event(1).eventId));check(q.state.events.size==1)
    store.fail=true
    try{q.add(event(2),now);error("Failed store accepted")}catch(_:java.io.IOException){}
    check(q.state.events.size==1);store.fail=false
    q=queue();check(q.state.collection==collection && q.state.events.size==1)
    q.acknowledge(binding,collection,setOf(event(1).eventId),setOf(event(1).eventId));check(q.state.events.isEmpty())
    check(q.add(event(1),now));check(q.state.events.isEmpty()) // recovered session cannot replay an acknowledged event
    for(i in 2..MeasurementBuffer.CAPACITY+1)check(q.add(event(i),now))
    check(!q.add(event(9999),now) && q.state.dropped==1L);check(q.batch(now).size==100)
    q.configure(binding,false,0);check(!q.state.enabled && q.state.events.isEmpty() && q.state.receipts.isEmpty())
    q.configure(binding,true,now+1);check(q.state.collection!=collection)
    q.add(event(2,now+1),now+1)
    q.acknowledge(binding,collection,setOf(event(2).eventId),setOf(event(2).eventId));check(q.state.events.size==1)
    q.batch(now+91L*86400000);check(q.state.events.isEmpty() && q.state.expired==1L)
    check(!q.add(event(3,now+92L*86400000),now))
    println("Measurement outbox consent/restart/idempotency/failure/capacity/expiry contracts PASS")
}
