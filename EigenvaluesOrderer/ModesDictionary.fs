﻿namespace EigenvaluesOrderer

type TmpCell(fullkey : (float * float) array, key_ev : float*float) =
    let _keys_full = fullkey
    let _ev : EigenValue option ref = ref None
    let _keys_ev = key_ev
    member x.FullKey with get() = _keys_full
    member x.KeysEV with get() = _keys_ev
    member x.EV with get() = !_ev and set(newval) = _ev := newval

type EigenDict (keys : string list, keys_full : (float * float) array list, keys_ev : (float * float) array, ev : EigenValue list) =
    let _log = ref ""
    let fullkey_dist (a : (float * float) array) (b : (float * float) array) =
        let sqr arg = arg * arg
        Array.map2 (fun (r1,i1) (r2,i2) -> (sqr (r1-r2)) + (sqr (i1-i2))) a b |> Array.sum |> sqrt
        //Array.map2 (fun (r1,i1) (r2,i2) -> sqr (r1-r2)) a b |> Array.sum |> sqrt
    // cur - index and value of currently chosen mode for given key; src mode distances list
    let rec min_index (cur : int*float) (src : float list) (available : bool ref array) i =
        match src with
            | head :: tail -> 
                if head <= (snd cur) && (available.[i].Value) then 
                    min_index (i, head) tail available (i+1) 
                else min_index cur tail available (i+1)
            | [] -> fst cur
    let min_indeces (dists : float [] []) =
        let find_min (I : int array) (J : int array) =
            let cur = ref (I.[0], J.[0])
            let curval = ref dists.[fst !cur].[snd !cur]
            for i in I do
                for j in J do
                    if dists.[i].[j] < !curval then 
                        cur := (i,j)
                        curval := dists.[i].[j]
            !cur
        let extract (ar : int array) k =
            try
                match (k, ar.Length) with
                    | (0, l) when l = 1 -> [||]
                    | (0, l) when l > 1 -> ar.[1..]
                    | (k, l) when k < (l - 1) -> Array.append ar.[..(k - 1)] ar.[(k + 1)..]
                    | (k, l) when k = (l - 1) -> ar.[..(k - 1)]
                    | _ -> failwith ("\ncannot extract chosen index:" + k.ToString() + "\n")
            with
                | _ -> failwith ("\ncannot extract chosen index:" + k.ToString() + "\n")
        let _I = ref [|0..(dists.Length - 1)|]
        let _J = ref [|0..(dists.Length - 1)|]
        let indeces = [|for i in 0..(dists.Length - 1) -> ref (0,0)|]
        for k in 0..(dists.Length - 1) do
            indeces.[k] := find_min !_I !_J
            _I := extract !_I (Array.findIndex ((=) (fst indeces.[k].Value)) !_I)
            _J := extract !_J (Array.findIndex ((=) (snd indeces.[k].Value)) !_J)
        indeces
    let (_dict, _keys, _keys_full, _all_distances) =
        if keys.IsEmpty then
            let loc_keys = List.map (fun (a : EigenValue) -> a.getStringHash()) ev
            (
                List.zip loc_keys ev |> dict,
                loc_keys,
                List.map (fun (a : EigenValue) -> a.V) ev,
                [|for i in 1..ev.Length -> [|for i in 1..ev.Length -> 0.0|]|])

        else
            //let wip = List.zip keys (List.map (fun a -> TmpCell(a)) keys_full) |> List.toSeq |> dict
            let eax = List.map2 (fun a b -> TmpCell(a, b)) keys_full (Array.toList keys_ev)
            let are_available = [|for i in 0..(eax.Length - 1) -> ref true|]
            let all_distances = // all_distances.[i].[j] :> i - eax index, j - ev index
//                [
//                    for i in 0..(ev.Length - 1) ->
//                        [
//                        for j in 0..(ev.Length - 1) ->
//                            fullkey_dist eax.[i].FullKey (Array.zip (fst ev.[j].V) (snd ev.[j].V))]]
                List.map
                    (fun (tmpcell : TmpCell) -> 
                        List.map (fun (eig : EigenValue) -> 
                            (fullkey_dist 
                                tmpcell.FullKey 
                                (Array.zip (fst eig.V) (snd eig.V))) +
                                sqrt ((((fst tmpcell.KeysEV) - eig.Re) ** 2.0) + (((snd tmpcell.KeysEV) - eig.Im) ** 2.0)) )
                            ev 
                        |> List.toArray)
                    eax
                |> List.toArray
            try
                Array.iter
                    (
                        fun (cell : (int*int) ref) ->
                            eax.[fst cell.Value].EV <- Some (ev.[snd cell.Value])
                            _log := !_log + "mode #" + (snd cell.Value).ToString() + 
                                " pushed to cell #" + (fst cell.Value).ToString() + 
                                "with distance " + all_distances.[(fst cell.Value)].[(snd cell.Value)].ToString() + "\n"
                            _log := !_log + "other distances are: \n" + 
                                (
                                    all_distances.[(snd cell.Value)] 
                                    |> Array.map (fun a -> a.ToString())
                                    |> Array.reduce (fun a b -> a + "\n" + b) ))
                    (min_indeces all_distances)
            with
                | _ -> 
                    failwith 
                        (
                            "Cannot reorder: \n" +
                            (Array.map 
                                (fun (cell : (int*int) ref) ->
                                    let (_to, _from) = !cell
                                    _to.ToString() + "<-" + _from.ToString() + "\n") 
                                (min_indeces all_distances)
                            |> Array.reduce (+)))
//            Array.iteri
//                (
//                    fun index (dist_vector : float array) -> // distances from current TmpCell to all subject eigenvalues
//                        let id = Array.findIndex (fun a -> !a) are_available
//                        let closest_id = min_index (id, dist_vector.[id]) dist_vector are_available 0
//                        are_available.[closest_id]  := false
//                        _log := !_log + "mode #" + closest_id.ToString() + " pushed to cell #" + index.ToString() + "with distance " + dist_vector.[closest_id].ToString() + "\n"
//                        _log := !_log + "other distances are: \n" + 
//                            (
//                                dist_vector 
//                                |> Array.map (fun a -> a.ToString())
//                                |> Array.reduce (fun a b -> a + "\n" + b) )
//                        eax.[index].EV <- Some (ev.[closest_id]))
//                all_distances
            (
                List.zip keys (List.map (fun (a : TmpCell) -> a.EV.Value) eax) |> dict,
                keys,
                List.map Array.unzip keys_full,
                all_distances)
            
    member x.EigenValues with get() = _dict
    member x.Keys with get() = _keys
    member x.KeysFull //with 
        with get() = //_keys_full
            List.map
                (fun (a : EigenValue) -> a.V)
                (List.map (fun (a : string) -> x.EigenValues.[a]) x.Keys)
    member x.Log with get() = _log.Value
    member x.AllDistaces with get() = _all_distances
