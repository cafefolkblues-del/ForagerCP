# -*- coding: utf-8 -*-
"""오늘 할 일을 디스코드 채널에 올린다.

일정의 원본은 GitHub 이슈 본문 첫 줄이다:
    **기간** 11/10 · **예상** 6h
    **기간** 11/2 ~ 11/3 · **예상** 8h
오늘 날짜가 그 기간에 들어가는 열린 이슈를 모아 웹훅으로 보낸다.
일정을 바꾸려면 이슈의 기간 줄만 고치면 된다 — 이 스크립트에는 날짜가 없다.

사용:
    python tools/discord_today.py --dry-run              # 보낼 내용만 출력
    python tools/discord_today.py --date 11/12 --dry-run # 다른 날 미리보기
    python tools/discord_today.py                        # 실제 전송

환경 변수:
    DISCORD_WEBHOOK_URL  채널 설정 → 연동 → 웹후크에서 만든 주소 (전송할 때만 필요)
    GITHUB_TOKEN         없으면 `gh auth token`을 쓴다
    TASK_ASSIGNEE        제목 앞 이름 필터 (기본 "장혁", 빈 값이면 전원)
"""
import argparse
import datetime as dt
import json
import os
import re
import subprocess
import sys
import urllib.error
import urllib.request

REPO = "cafefolkblues-del/ForagerCP"
PROJECT_URL = "https://github.com/users/cafefolkblues-del/projects/1"
KST = dt.timezone(dt.timedelta(hours=9))
WEEKDAY = "월화수목금토일"

PERIOD = re.compile(r"\*\*기간\*\*\s*(\d{1,2})/(\d{1,2})(?:\s*~\s*(\d{1,2})/(\d{1,2}))?")
ESTIMATE = re.compile(r"\*\*예상\*\*\s*([0-9.]+)\s*h")
TODO = re.compile(r"^- \[ \] (.+)$", re.M)


def token():
    if os.environ.get("GITHUB_TOKEN"):
        return os.environ["GITHUB_TOKEN"]
    try:
        return subprocess.run(["gh", "auth", "token"], capture_output=True, text=True, check=True).stdout.strip()
    except Exception:
        sys.exit("GITHUB_TOKEN도 없고 gh 로그인도 안 되어 있습니다.")


def open_issues():
    issues, page = [], 1
    while True:
        req = urllib.request.Request(
            f"https://api.github.com/repos/{REPO}/issues?state=open&per_page=100&page={page}",
            headers={"Authorization": f"Bearer {token()}", "Accept": "application/vnd.github+json",
                     "User-Agent": "foragercp-daily-tasks"})
        with urllib.request.urlopen(req) as r:
            batch = json.load(r)
        for i in batch:
            i["body"] = (i.get("body") or "").replace("\r", "")
        issues += [i for i in batch if "pull_request" not in i]
        if len(batch) < 100:
            return issues
        page += 1


def period_of(body, year):
    m = PERIOD.search(body or "")
    if not m:
        return None
    start = dt.date(year, int(m[1]), int(m[2]))
    end = dt.date(year, int(m[3]), int(m[4])) if m[3] else start
    return start, end


def todays(day, assignee):
    picked = []
    for i in open_issues():
        if assignee and not i["title"].startswith(assignee):
            continue
        p = period_of(i["body"], day.year)
        if not p or not (p[0] <= day <= p[1]):
            continue
        est = ESTIMATE.search(i["body"] or "")
        picked.append({
            "number": i["number"], "title": i["title"], "url": i["html_url"],
            "hours": float(est[1]) if est else 0.0,
            "labels": [l["name"] for l in i["labels"]],
            "todo": TODO.findall(i["body"] or "")[:3],
            "multi_day": p[0] != p[1],
        })
    return sorted(picked, key=lambda x: x["number"])


def message(day, tasks, assignee):
    who = f"{assignee} · " if assignee else ""
    head = f"{day.month}/{day.day} ({WEEKDAY[day.weekday()]})"
    if not tasks:
        return {"content": f"📋 **{who}{head} 할 일** — 잡힌 이슈가 없습니다. [작업 보드]({PROJECT_URL})"}

    total = sum(t["hours"] for t in tasks)
    fields = []
    for t in tasks:
        title = re.sub(r"^\S+ - ", "", t["title"])
        flags = [l for l in t["labels"] if l.startswith("대기:") or l == "기획 확인"]
        extra = (" · 여러 날" if t["multi_day"] else "") + (f" · ⚠️ {', '.join(flags)}" if flags else "")
        detail = "\n".join(f"• {x}" for x in t["todo"]) or "—"
        fields.append({
            "name": f"#{t['number']} {title} ({t['hours']:g}h{extra})"[:256],
            "value": f"{detail}\n[이슈 열기]({t['url']})"[:1024],
        })
    return {
        "embeds": [{
            "title": f"📋 {who}{head} 할 일 — {len(tasks)}개 · 예상 {total:g}h",
            "url": PROJECT_URL,
            "color": 0xB4532A,
            "fields": fields[:25],
            "footer": {"text": "기간은 GitHub 이슈 첫 줄 기준 · 바꾸려면 이슈를 고치세요"},
        }]
    }


FORUM_NEEDS_THREAD = 220001  # 포럼 채널 웹후크는 글 제목(thread_name)이 있어야 한다


def post(url, payload):
    req = urllib.request.Request(url, data=json.dumps(payload).encode("utf-8"), method="POST",
                                 headers={"Content-Type": "application/json", "User-Agent": "foragercp-daily-tasks"})
    try:
        with urllib.request.urlopen(req):
            return None
    except urllib.error.HTTPError as e:
        try:
            return json.loads(e.read().decode("utf-8"))
        except Exception:
            return {"message": f"HTTP {e.code}"}


def send(payload, thread_name):
    """일반 채널이면 그대로, 포럼 채널이면 날짜 제목으로 새 글을 만들어 올린다."""
    url = os.environ.get("DISCORD_WEBHOOK_URL")
    if not url:
        sys.exit("DISCORD_WEBHOOK_URL이 없습니다. --dry-run으로 미리보기만 하거나 환경 변수를 넣어 주세요.")
    err = post(url, payload)
    if err and err.get("code") == FORUM_NEEDS_THREAD:
        err = post(url, {**payload, "thread_name": thread_name[:100]})
    if err:
        sys.exit(f"디스코드 전송 실패: {err}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--date", help="M/D 형식. 없으면 오늘(한국 시간)")
    ap.add_argument("--dry-run", action="store_true", help="보내지 않고 내용만 출력")
    args = ap.parse_args()

    today = dt.datetime.now(KST).date()
    if args.date:
        m, d = map(int, args.date.split("/"))
        today = dt.date(today.year, m, d)
    assignee = os.environ.get("TASK_ASSIGNEE", "장혁")

    payload = message(today, todays(today, assignee), assignee)
    if args.dry_run:
        sys.stdout.reconfigure(encoding="utf-8")
        print(json.dumps(payload, ensure_ascii=False, indent=2))
    else:
        send(payload, f"📋 {today.month}/{today.day} ({WEEKDAY[today.weekday()]}) 할 일")
        print("보냈습니다.")


if __name__ == "__main__":
    main()
