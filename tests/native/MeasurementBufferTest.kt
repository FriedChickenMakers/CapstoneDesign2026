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
    check(!q.add(event(1),now));check(q.batchSnapshot(now).completeThrough==0L);q.configure(binding,true,now)
    val collection=q.state.collection
    check(!q.add(event(2,now-1),now));check(q.add(event(1),now));check(q.add(event(1),now));check(q.state.events.size==1)
    check(q.batch(now).size==1)
    check(q.batchSnapshot(now).completeThrough==now)
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
    check(q.batchSnapshot(now).completeThrough==0L)
    while(q.state.events.size>100) {
        val sent=q.batchSnapshot(now)
        check(sent.completeThrough==0L)
        val ids=sent.events.map {it.eventId}.toSet()
        q.acknowledge(binding,collection,ids,ids)
    }
    val finalBatch=q.batchSnapshot(now+1000)
    check(finalBatch.events.isNotEmpty() && finalBatch.completeThrough==now+1000)
    val finalIds=finalBatch.events.map {it.eventId}.toSet()
    q.acknowledge(binding,collection,finalIds,finalIds)
    check(q.batchSnapshot(now+2000).events.isEmpty() && q.batchSnapshot(now+2000).completeThrough==now+2000)
    q.configure(binding,false,0);check(!q.state.enabled && q.state.events.isEmpty() && q.state.receipts.isEmpty())
    q.configure(binding,true,now+1);check(q.state.collection!=collection)
    q.add(event(2,now+1),now+1)
    q.acknowledge(binding,collection,setOf(event(2).eventId),setOf(event(2).eventId));check(q.state.events.size==1)
    q.batch(now+91L*86400000);check(q.state.events.isEmpty() && q.state.expired==1L)
    check(!q.add(event(3,now+92L*86400000),now))
    val skewStore=Store();val skew=MeasurementBuffer(skewStore){"f".repeat(32)}
    skew.configure(binding,true,now-1000)
    check(skew.add(event(21,now+86400000),now+86400000)) // forward clock before correction
    check(skew.add(event(22,now),now))
    val corrected=skew.batchSnapshot(now)
    check(corrected.events.map{it.eventId}==listOf(event(22).eventId))
    check(corrected.completeThrough==0L && skew.state.events.size==2) // retained, not silently discarded
    val correctedIds=corrected.events.map{it.eventId}.toSet()
    skew.acknowledge(binding,skew.state.collection,correctedIds,correctedIds)
    check(skew.batchSnapshot(now).events.isEmpty() && skew.batchSnapshot(now).completeThrough==0L)
    val catchup=skew.batchSnapshot(now+86400000)
    check(catchup.events.single().eventId==event(21).eventId && catchup.completeThrough==now+86400000)
    val reloaded=MeasurementBuffer(skewStore){"e".repeat(32)}
    check(reloaded.batchSnapshot(now).events.isEmpty() && reloaded.state.events.size==1)
    println("Measurement outbox consent/restart/idempotency/failure/capacity/expiry contracts PASS")
}
