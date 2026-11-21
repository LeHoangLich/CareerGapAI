import {useSyncExternalStore} from 'react';
export type RecruitmentSignal={total:number;unread:number;revision:string};
type Live=RecruitmentSignal&{userId:string|undefined;connection:'connecting'|'connected'|'reconnecting'|'offline'};
const empty:Live={userId:undefined,total:0,unread:0,revision:'',connection:'offline'};
let current:Live=empty;
const listeners=new Set<()=>void>();
const subscribe=(listener:()=>void)=>{listeners.add(listener);return()=>{listeners.delete(listener)}};
const snapshot=()=>current;
export function useRecruitmentLive(userId?:string){const value=useSyncExternalStore(subscribe,snapshot,snapshot);return value.userId===userId?value:empty;}
function publish(next:Live){if(JSON.stringify(next)===JSON.stringify(current))return;current=next;listeners.forEach(fn=>fn())}
export function recruitmentConnection(userId:string,connection:Live['connection']){publish(current.userId===userId?{...current,connection}:{...empty,userId,connection})}
export function recruitmentDisconnected(userId:string){if(current.userId===userId)publish(empty)}
export function recruitmentSignal(userId:string,input:unknown){
 if(current.userId!==userId||!input||typeof input!=='object')return false;const value=input as RecruitmentSignal;
 if(!Number.isSafeInteger(value.total)||!Number.isSafeInteger(value.unread)||value.total<0||value.unread<0||value.unread>value.total||typeof value.revision!=='string'||!/^[a-f0-9]{64}$/.test(value.revision))return false;
 publish({userId,total:value.total,unread:value.unread,revision:value.revision,connection:'connected'});return true;
}
export function recruitmentSnapshot(){return current;}
