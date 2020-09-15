import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';


@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss']
})
export class HomeComponent implements OnInit {

  events = [];
  signupTest: string = "";

  constructor(private router: Router, private httpClient: HttpClient) { }


  ngOnInit() {
    var url = '/api/computerreset/api/siteup';
    const promise =  this.httpClient.get(url, {responseType: 'text'}).toPromise()
      .then(data => this.signupTest = data);
  }

}