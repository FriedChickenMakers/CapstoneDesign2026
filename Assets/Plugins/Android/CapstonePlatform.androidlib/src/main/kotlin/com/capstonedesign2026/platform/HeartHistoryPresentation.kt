package com.capstonedesign2026.platform

/** Display copy only. Does not infer health state or change the queried records. */
internal object HeartHistoryPresentation {
    data class Notice(val title: String, val detail: String, val warning: Boolean = false,
                      val returnToPermissions: Boolean = false)

    fun notice(status: String?): Notice? = when (status) {
        null -> Notice("기록을 확인하고 있어요", "동기화된 심박 기록을 불러오는 중이에요.")
        "AVAILABLE" -> null
        "STALE" -> Notice("이전 측정 기록이에요", "마지막 측정 시각을 확인해 주세요. 실시간 심박수가 아니에요.")
        "PARTIAL" -> Notice("일부 기록만 확인했어요", "아래 평균은 잠정 값이에요. 다시 조회해 주세요.", warning = true)
        "PERMISSION_DENIED", "PERMISSION_REQUIRED" -> Notice("심박 기록 권한이 필요해요",
            "설정의 ‘건강 데이터 권한 확인’에서 허용해 주세요.", warning = true, returnToPermissions = true)
        "UNSUPPORTED", "SERVICE_UNAVAILABLE" -> Notice("건강 기록을 연결할 수 없어요",
            "이 기기에서는 Health Connect를 사용할 수 없어요.", warning = true)
        "NO_DATA" -> Notice("아직 심박 기록이 없어요", "최근 24시간의 기록이 없어요. 건강 앱을 동기화한 뒤 다시 확인해 주세요.")
        else -> Notice("기록을 불러오지 못했어요", "잠시 후 다시 조회해 주세요.", warning = true)
    }

    fun sourceLabel(source: String): String = source.split(" · ").joinToString(" · ") { part ->
        when (part) {
            "Samsung Health" -> "삼성 헬스"
            "device unspecified" -> "측정 기기 정보 없음"
            "watch" -> "워치"
            "phone" -> "휴대전화"
            "ring" -> "반지형 기기"
            "fitness band" -> "활동량 밴드"
            "chest strap" -> "가슴 착용 기기"
            else -> part
        }
    }
}
